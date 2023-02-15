using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO.Abstractions;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Neo.Cryptography;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using Newtonsoft.Json;
using static Neo.BlockchainToolkit.Constants;
using static Neo.BlockchainToolkit.Utility;

using NeoArray = Neo.VM.Types.Array;
using NeoStruct = Neo.VM.Types.Struct;

namespace NeoWorkNet.Commands;

[Command("create", Description = "Create a Neo-Worknet branch")]
class CreateCommand
{
    readonly IFileSystem fs;

    public CreateCommand(IFileSystem fileSystem)
    {
        this.fs = fileSystem;
    }

    [Argument(0, Description = "URL of Neo JSON-RPC Node. Specify MainNet, TestNet or JSON-RPC URL")]
    [Required]
    internal string RpcUri { get; } = string.Empty;

    [Argument(1, Description = "Name of " + WORKNET_EXTENSION + " file to create (Default: ./" + DEFAULT_WORKNET_FILENAME + ")")]
    internal string Output { get; set; } = string.Empty;

    [Option(Description = "Block height to branch at")]
    internal uint Index { get; } = 0;

    [Option(Description = "Overwrite existing data")]
    internal bool Force { get; set; }

    [Option("--disable-log", Description = "Disable verbose data logging")]
    internal bool DisableLog { get; set; }

    internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
    {
        try
        {
            if (!DisableLog)
            {
                var stateServiceObserver = new KeyValuePairObserver(Utility.GetDiagnosticWriter(console));
                var diagnosticObserver = new DiagnosticObserver(StateServiceStore.LoggerCategory, stateServiceObserver);
                DiagnosticListener.AllListeners.Subscribe(diagnosticObserver);
            }

            if (!TryParseRpcUri(RpcUri, out var uri))
            {
                throw new ArgumentException($"Invalid RpcUri value \"{RpcUri}\"");
            }

            var filename = fs.ResolveWorkNetFileName(Output);
            if (fs.File.Exists(filename) && !Force) throw new Exception($"{filename} already exists");
            var dataDir = fs.GetWorknetDataDirectory(filename);
            if (fs.Directory.Exists(dataDir) && !Force) throw new Exception($"{dataDir} already exists");

            console.WriteLine($"Retrieving branch information from {RpcUri}");
            using var rpcClient = new RpcClient(uri);
            var index = Index;
            if (index == 0)
            {
                var stateApi = new StateAPI(rpcClient);
                var (localRootIndex, validatedRootIndex) = await stateApi.GetStateHeightAsync().ConfigureAwait(false);
                index = validatedRootIndex ?? localRootIndex ?? throw new Exception("No valid root index available");
            }
            var branchInfo = await StateServiceStore.GetBranchInfoAsync(rpcClient, index).ConfigureAwait(false);

            console.WriteLine($"Initializing local worknet");

            var consensusWallet = new ToolkitWallet("consensus", branchInfo.ProtocolSettings);
            var consensusAccount = consensusWallet.CreateAccount();
            consensusAccount.IsDefault = true;

            fs.SaveWorknetFile(filename, uri, branchInfo, consensusWallet);

            if (fs.Directory.Exists(dataDir))
            {
                if (!Force) throw new Exception($"{dataDir} already exists");
                fs.Directory.Delete(dataDir, true);
            }
            fs.Directory.CreateDirectory(dataDir);

            using var db = RocksDbUtility.OpenDb(dataDir);
            using var stateStore = new StateServiceStore(uri, branchInfo, db);
            using var trackStore = new PersistentTrackingStore(db, stateStore);

            InitializeStore(trackStore, consensusAccount);

            console.WriteLine($"Created {filename}");
            console.WriteLine("    Note: The private keys for the accounts in this file are are *not* encrypted.");
            console.WriteLine("          Do not use these accounts on MainNet or in any other system where security is a concern.");
            return 0;
        }
        catch (Exception ex)
        {
            app.WriteException(ex);
            return 1;
        }
    }

    internal static void InitializeStore(IStore store, params WalletAccount[] consensusAccounts)
        => InitializeStore(store, (IEnumerable<WalletAccount>)consensusAccounts);

    internal static void InitializeStore(IStore store, IEnumerable<WalletAccount> consensusAccounts)
    {
        const byte Prefix_Block = 5;
        const byte Prefix_BlockHash = 9;
        const byte Prefix_Candidate = 33;
        const byte Prefix_Committee = 14;
        const byte Prefix_CurrentBlock = 12;

        var keys = consensusAccounts.Select(a => a.GetKey().PublicKey).ToArray();
        var signerCount = (keys.Length * 2 / 3) + 1;
        var consensusContract = Contract.CreateMultiSigContract(signerCount, keys);

        using var snapshot = new SnapshotCache(store.GetSnapshot());

        // replace the Neo Committee with express consensus nodes
        // Prefix_Committee stores array of structs containing PublicKey / vote count 
        var members = consensusAccounts.Select(a => new NeoStruct { a.GetKey().PublicKey.ToArray(), 0 });
        var committee = new NeoArray(members);
        var committeeKeyBuilder = new KeyBuilder(NativeContract.NEO.Id, Prefix_Committee);
        var committeeItem = snapshot.GetAndChange(committeeKeyBuilder);
        committeeItem.Value = BinarySerializer.Serialize(committee, 1024 * 1024);

        // remove existing candidates (Prefix_Candidate) to ensure that 
        // worknet node account doesn't get outvoted
        var candidateKeyBuilder = new KeyBuilder(NativeContract.NEO.Id, Prefix_Candidate);
        foreach (var (key, value) in snapshot.Find(candidateKeyBuilder.ToArray()))
        {
            snapshot.Delete(key);
        }

        // create an *UNSIGNED* block that will be appended to the chain 
        // with updated NextConsensus field.
        var prevHash = NativeContract.Ledger.CurrentHash(snapshot);
        var prevBlock = NativeContract.Ledger.GetHeader(snapshot, prevHash);

        var trimmedBlock = new TrimmedBlock
        {
            Header = new Header
            {
                Version = 0,
                PrevHash = prevBlock.Hash,
                MerkleRoot = MerkleTree.ComputeRoot(Array.Empty<UInt256>()),
                Timestamp = Math.Max(Neo.Helper.ToTimestampMS(DateTime.UtcNow), prevBlock.Timestamp + 1),
                Index = prevBlock.Index + 1,
                PrimaryIndex = 0,
                NextConsensus = consensusContract.ScriptHash,
                Witness = new Witness()
                {
                    InvocationScript = Array.Empty<byte>(),
                    VerificationScript = Array.Empty<byte>()
                }
            },
            Hashes = Array.Empty<UInt256>(),
        };

        // update Prefix_BlockHash (mapping index -> hash)
        var blockHashKey = new KeyBuilder(NativeContract.Ledger.Id, Prefix_BlockHash).AddBigEndian(trimmedBlock.Index);
        snapshot.Add(blockHashKey, new StorageItem(trimmedBlock.Hash.ToArray()));

        // update Prefix_Block (store block indexed by hash)
        var blockKey = new KeyBuilder(NativeContract.Ledger.Id, Prefix_Block).Add(trimmedBlock.Hash);
        snapshot.Add(blockKey, new StorageItem(trimmedBlock.ToArray()));

        // update Prefix_CurrentBlock (struct containing current block hash + index)
        var curBlockKey = new KeyBuilder(NativeContract.Ledger.Id, Prefix_CurrentBlock);
        var currentBlock = new Neo.VM.Types.Struct() { trimmedBlock.Hash.ToArray(), trimmedBlock.Index };
        var currentBlockItem = snapshot.GetAndChange(curBlockKey);
        currentBlockItem.Value = BinarySerializer.Serialize(currentBlock, 1024 * 1024);

        snapshot.Commit();
    }
}