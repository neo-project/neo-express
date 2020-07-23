using Neo;
using Neo.IO;
using Neo.IO.Caching;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.Seattle.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.VM;
using Neo.Wallets;
using NeoExpress.Abstractions.Models;
using NeoExpress.Neo3.Models;
using NeoExpress.Neo3.Node;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nito.Disposables;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NeoExpress.Neo3
{
    public class BlockchainOperations
    {
        public ExpressChain CreateBlockchain(FileInfo output, int count, TextWriter writer)
        {
            if (File.Exists(output.FullName))
            {
                throw new ArgumentException($"{output.FullName} already exists", nameof(output));
            }

            if (count != 1 && count != 4 && count != 7)
            {
                throw new ArgumentException("invalid blockchain node count", nameof(count));
            }

            var chain = BlockchainOperations.CreateBlockchain(count);

            writer.WriteLine($"Created {count} node privatenet at {output.FullName}");
            writer.WriteLine("    Note: The private keys for the accounts in this file are are *not* encrypted.");
            writer.WriteLine("          Do not use these accounts on MainNet or in any other system where security is a concern.");

            return chain;
        }

        public byte[] ToScriptHashByteArray(ExpressWalletAccount account)
        {
            var devAccount = DevWalletAccount.FromExpressWalletAccount(account);
            return devAccount.ScriptHash.ToArray();
        }

        public void ResetNode(ExpressChain chain, int index)
        {
            if (index >= chain.ConsensusNodes.Count)
            {
                throw new ArgumentException(nameof(index));
            }

            var node = chain.ConsensusNodes[index];
            var folder = node.GetBlockchainPath();

            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }

        static ExpressChain CreateBlockchain(int count)
        {
            var wallets = new List<(DevWallet wallet, Neo.Wallets.WalletAccount account)>(count);

            static ushort GetPortNumber(int index, ushort portNumber) => (ushort)(49000 + (index * 1000) + portNumber);

            for (var i = 1; i <= count; i++)
            {
                var wallet = new DevWallet($"node{i}");
                var account = wallet.CreateAccount();
                account.IsDefault = true;
                wallets.Add((wallet, account));
            }

            var keys = wallets.Select(t => t.account.GetKey().PublicKey).ToArray();

            var contract = Contract.CreateMultiSigContract((keys.Length * 2 / 3) + 1, keys);

            foreach (var (wallet, account) in wallets)
            {
                var multiSigContractAccount = wallet.CreateAccount(contract, account.GetKey());
                multiSigContractAccount.Label = "MultiSigContract";
            }

            // 49152 is the first port in the "Dynamic and/or Private" range as specified by IANA
            // http://www.iana.org/assignments/port-numbers
            var nodes = new List<ExpressConsensusNode>(count);
            for (var i = 0; i < count; i++)
            {
                nodes.Add(new ExpressConsensusNode()
                {
                    TcpPort = GetPortNumber(i, 333),
                    WebSocketPort = GetPortNumber(i, 334),
                    RpcPort = GetPortNumber(i, 332),
                    Wallet = wallets[i].wallet.ToExpressWallet()
                });
            }

            return new ExpressChain()
            {
                Magic = ExpressChain.GenerateMagicValue(),
                ConsensusNodes = nodes,
            };
        }

        private const string GENESIS = "genesis";

        static bool EqualsIgnoreCase(string a, string b)
            => string.Equals(a, b, StringComparison.InvariantCultureIgnoreCase);

        public ExpressWallet CreateWallet(ExpressChain chain, string name)
        {
            bool IsReservedName()
            {
                if (EqualsIgnoreCase(GENESIS, name))
                    return true;

                foreach (var node in chain.ConsensusNodes)
                {
                    if (EqualsIgnoreCase(name, node.Wallet.Name))
                        return true;
                }

                return false;
            }

            if (IsReservedName())
            {
                throw new Exception($"{name} is a reserved name. Choose a different wallet name.");
            }

            var wallet = new DevWallet(name);
            var account = wallet.CreateAccount();
            account.IsDefault = true;
            return wallet.ToExpressWallet();
        }

        public async Task RunBlockchainAsync(ExpressChain chain, int index, uint secondsPerBlock, bool discard, TextWriter writer, CancellationToken cancellationToken)
        {
            if (index >= chain.ConsensusNodes.Count)
            {
                throw new ArgumentException(nameof(index));
            }

            var node = chain.ConsensusNodes[index];
            var folder = node.GetBlockchainPath();
            writer.WriteLine(folder);

            if (!NodeUtility.InitializeProtocolSettings(chain, secondsPerBlock))
            {
                throw new Exception("could not initialize protocol settings");
            }

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            // create a named mutex so that checkpoint create command
            // can detect if blockchain is running automatically
            var multiSigAccount = node.Wallet.Accounts.Single(a => a.IsMultiSigContract());
            using var mutex = new Mutex(true, multiSigAccount.ScriptHash);

            using var store = GetStore();
            await NodeUtility.RunAsync(store, node, writer, cancellationToken).ConfigureAwait(false);

            Neo.Persistence.IStore GetStore()
            {
                if (discard)
                {
                    try 
                    {
                        var rocksDbStore = RocksDbStore.OpenReadOnly(folder);
                        return new CheckpointStore(rocksDbStore);
                    }
                    catch
                    {
                        return new CheckpointStore(new NullReadOnlyStore());
                    }
                }
                else
                {
                    return RocksDbStore.Open(folder);
                }
            }
        }

        class NullReadOnlyStore : Neo.Persistence.IReadOnlyStore
        {
            public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte table, byte[] key, SeekDirection direction)
                => Enumerable.Empty<(byte[] Key, byte[] Value)>();

            public byte[]? TryGet(byte table, byte[]? key) => null;
        }

        public async Task RunCheckpointAsync(ExpressChain chain, string checkPointArchive, uint secondsPerBlock, TextWriter writer, CancellationToken cancellationToken)
        {
            string checkpointTempPath;
            do
            {
                checkpointTempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            }
            while (Directory.Exists(checkpointTempPath));

            using var folderCleanup = AnonymousDisposable.Create(() => {
                if (Directory.Exists(checkpointTempPath))
                {
                    Directory.Delete(checkpointTempPath, true);
                }
            });

            if (!NodeUtility.InitializeProtocolSettings(chain, secondsPerBlock))
            {
                throw new Exception("could not initialize protocol settings");
            }

            if (chain.ConsensusNodes.Count != 1)
            {
                throw new ArgumentException("Checkpoint restore is only supported on single node express instances", nameof(chain));
            }

            var node = chain.ConsensusNodes[0];
            var multiSigAccount = node.Wallet.Accounts.Single(a => a.IsMultiSigContract());
            RocksDbStore.RestoreCheckpoint(checkPointArchive, checkpointTempPath, chain.Magic, multiSigAccount.ScriptHash);

            // create a named mutex so that checkpoint create command
            // can detect if blockchain is running automatically
            using var mutex = new Mutex(true, multiSigAccount.ScriptHash);

            using var rocksDbStore = RocksDbStore.OpenReadOnly(checkpointTempPath);
            using var checkpointStore = new CheckpointStore(rocksDbStore); 
            await NodeUtility.RunAsync(checkpointStore, node, writer, cancellationToken).ConfigureAwait(false);
        }

        public async Task CreateCheckpoint(ExpressChain chain, string checkPointFileName, TextWriter writer)
        {
            static bool NodeRunning(ExpressConsensusNode node)
            {
                // Check to see if there's a neo-express blockchain currently running
                // by attempting to open a mutex with the multisig account address for 
                // a name. If so, do an online checkpoint instead of offline.

                var multiSigAccount = node.Wallet.Accounts.Single(a => a.IsMultiSigContract());

                return Mutex.TryOpenExisting(multiSigAccount.ScriptHash, out var _);
            }

            if (File.Exists(checkPointFileName))
            {
                throw new ArgumentException("Checkpoint file already exists", nameof(checkPointFileName));
            }

            if (chain.ConsensusNodes.Count != 1)
            {
                throw new ArgumentException("Checkpoint create is only supported on single node express instances", nameof(chain));
            }

            var node = chain.ConsensusNodes[0];
            var folder = node.GetBlockchainPath();

            if (NodeRunning(node))
            {
                var uri = chain.GetUri();
                var rpcClient = new RpcClient(uri.ToString());
                await rpcClient.RpcSendAsync("expresscreatecheckpoint", checkPointFileName).ConfigureAwait(false);
                writer.WriteLine($"Created {Path.GetFileName(checkPointFileName)} checkpoint online");
            }
            else
            {
                var multiSigAccount = node.Wallet.Accounts.Single(a => a.IsMultiSigContract());
                using var db = RocksDbStore.Open(folder);
                db.CreateCheckpoint(checkPointFileName, chain.Magic, multiSigAccount.ScriptHash);
                writer.WriteLine($"Created {Path.GetFileName(checkPointFileName)} checkpoint offline");
            }
        }

        private const string ADDRESS_FILENAME = "ADDRESS.neo-express";
        private const string CHECKPOINT_EXTENSION = ".nxp3-checkpoint";

        public string ResolveCheckpointFileName(string checkPointFileName)
        {
            checkPointFileName = string.IsNullOrEmpty(checkPointFileName)
                ? $"{DateTimeOffset.Now:yyyyMMdd-hhmmss}{CHECKPOINT_EXTENSION}"
                : checkPointFileName;

            if (!Path.GetExtension(checkPointFileName).Equals(CHECKPOINT_EXTENSION))
            {
                checkPointFileName += CHECKPOINT_EXTENSION;
            }

            return Path.GetFullPath(checkPointFileName);
        }

        public void RestoreCheckpoint(ExpressChain chain, string checkPointArchive, bool force)
        {
            string checkpointTempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            try
            {
                if (chain.ConsensusNodes.Count != 1)
                {
                    throw new ArgumentException("Checkpoint restore is only supported on single node express instances", nameof(chain));
                }

                var node = chain.ConsensusNodes[0];
                var multiSigAccount = node.Wallet.Accounts.Single(a => a.IsMultiSigContract());
                var blockchainDataPath = node.GetBlockchainPath();

                if (!force && Directory.Exists(blockchainDataPath))
                {
                    throw new Exception("You must specify force to restore a checkpoint to an existing blockchain.");
                }

                RocksDbStore.RestoreCheckpoint(checkPointArchive, checkpointTempPath, chain.Magic, multiSigAccount.ScriptHash);

                if (Directory.Exists(blockchainDataPath))
                {
                    Directory.Delete(blockchainDataPath, true);
                }

                Directory.Move(checkpointTempPath, blockchainDataPath);
            }
            finally
            {
                if (Directory.Exists(checkpointTempPath))
                {
                    Directory.Delete(checkpointTempPath, true);
                }
            }
        }

        static IEnumerable<ExpressWalletAccount> GetMultiSigAccounts(ExpressChain chain, string scriptHash)
        {
            return chain.ConsensusNodes
                .Select(n => n.Wallet)
                .Concat(chain.Wallets)
                .Select(w => w.Accounts.Find(a => a.ScriptHash == scriptHash))
                .Where(a => a != null);
        }

        // https://github.com/neo-project/docs/blob/release-neo3/docs/en-us/tooldev/sdk/transaction.md
        public UInt256 Transfer(ExpressChain chain, string asset, string quantity, ExpressWalletAccount sender, ExpressWalletAccount receiver)
        {
            if (!NodeUtility.InitializeProtocolSettings(chain))
            {
                throw new Exception("could not initialize protocol settings");
            }

            var uri = chain.GetUri();
            var rpcClient = new RpcClient(uri.ToString());

            var assetHash = NodeUtility.GetAssetId(asset);
            var amount = GetAmount();

            var senderHash = sender.GetScriptHashAsUInt160();
            var receiverHash = receiver.GetScriptHashAsUInt160();

            var script = assetHash.MakeScript("transfer", senderHash, receiverHash, amount);
            var signers = new[] { new Signer { Scopes = WitnessScope.CalledByEntry, Account = senderHash } };

            var tm = new TransactionManager(rpcClient)
                .MakeTransaction(script, signers)
                .AddSignatures(chain, sender)
                .Sign();

            return rpcClient.SendRawTransaction(tm.Tx);

            BigInteger GetAmount()
            {
                var nep5client = new Nep5API(rpcClient);
                if ("all".Equals(quantity, StringComparison.InvariantCultureIgnoreCase))
                {
                    return nep5client.BalanceOf(assetHash, sender.ScriptHash.ToScriptHash());
                }

                if (decimal.TryParse(quantity, out var value))
                {
                    var decimals = nep5client.Decimals(assetHash);
                    return value.ToBigInteger(decimals);
                }

                throw new Exception("invalid quantity");
            }
        }

        // https://github.com/neo-project/docs/blob/release-neo3/docs/en-us/tooldev/sdk/contract.md
        // https://github.com/ProDog/NEO-Test/blob/master/RpcClientTest/Test_ContractClient.cs#L38
        public async Task<UInt256> DeployContract(ExpressChain chain, string contract, ExpressWalletAccount account)
        {
            if (!NodeUtility.InitializeProtocolSettings(chain))
            {
                throw new Exception("could not initialize protocol settings");
            }

            var uri = chain.GetUri();
            var rpcClient = new RpcClient(uri.ToString());

            var (nefFile, manifest) = await LoadContract(contract).ConfigureAwait(false);
            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitSysCall(ApplicationEngine.System_Contract_Create, nefFile.Script, manifest.ToString());
                script = sb.ToArray();
            }

            var accountHash = account.GetScriptHashAsUInt160();
            var signers = new[] { new Signer { Scopes = WitnessScope.CalledByEntry, Account = accountHash } };

            var tm = new TransactionManager(rpcClient)
                .MakeTransaction(script, signers)
                .AddSignatures(chain, account)
                .Sign();

            return rpcClient.SendRawTransaction(tm.Sign().Tx);

            static async Task<(NefFile nefFile, ContractManifest manifest)> LoadContract(string contractPath)
            {
                var nefTask = Task.Run(() =>
                {
                    using var stream = File.OpenRead(contractPath);
                    using var reader = new BinaryReader(stream, Encoding.UTF8, false);
                    return reader.ReadSerializable<NefFile>();
                });

                var manifestTask = File.ReadAllBytesAsync(Path.ChangeExtension(contractPath, ".manifest.json"))
                    .ContinueWith(t => ContractManifest.Parse(t.Result), TaskContinuationOptions.OnlyOnRanToCompletion);

                await Task.WhenAll(nefTask, manifestTask).ConfigureAwait(false);
                return (nefTask.Result, manifestTask.Result);
            }
        }

        private static async Task<byte[]> LoadInvocationFileScript(string invocationFilePath)
        {
            JObject json;
            {
                using var fileStream = File.OpenRead(invocationFilePath);
                using var textReader = new StreamReader(fileStream);
                using var jsonReader = new JsonTextReader(textReader);
                json = await JObject.LoadAsync(jsonReader).ConfigureAwait(false);
            }

            var scriptHash = UInt160.Parse(json.Value<string>("hash"));
            var operation = json.Value<string>("operation");
            var args = ContractParameterParser.ParseParams(json.GetValue("args")).ToArray();

            using var sb = new ScriptBuilder();
            sb.EmitAppCall(scriptHash, operation, args);
            return sb.ToArray();
        }

        public async Task<UInt256> InvokeContract(ExpressChain chain, string invocationFilePath, ExpressWalletAccount account)
        {
            if (!NodeUtility.InitializeProtocolSettings(chain))
            {
                throw new Exception("could not initialize protocol settings");
            }

            var uri = chain.GetUri();
            var rpcClient = new RpcClient(uri.ToString());
            var script = await LoadInvocationFileScript(invocationFilePath).ConfigureAwait(false);

            var devAccount = DevWalletAccount.FromExpressWalletAccount(account);
            var signers = new[] { new Signer { Scopes = WitnessScope.CalledByEntry, Account = devAccount.ScriptHash } };

            var tm = new TransactionManager(rpcClient)
                .MakeTransaction(script, signers)
                // .AddSignatures(chain, devAccount)
                .Sign();

            return rpcClient.SendRawTransaction(tm.Tx);
        }

        public async Task<RpcInvokeResult> TestInvokeContract(ExpressChain chain, string invocationFilePath)
        {
            if (!NodeUtility.InitializeProtocolSettings(chain))
            {
                throw new Exception("could not initialize protocol settings");
            }

            var uri = chain.GetUri();
            var rpcClient = new RpcClient(uri.ToString());
            var script = await LoadInvocationFileScript(invocationFilePath).ConfigureAwait(false);
            return rpcClient.InvokeScript(script);
        }

        public ExpressWalletAccount? GetAccount(ExpressChain chain, string name)
        {
            var wallet = (chain.Wallets ?? Enumerable.Empty<ExpressWallet>())
                .SingleOrDefault(w => name.Equals(w.Name, StringComparison.InvariantCultureIgnoreCase));
            if (wallet != null)
            {
                return wallet.DefaultAccount;
            }

            var node = chain.ConsensusNodes
                .SingleOrDefault(n => name.Equals(n.Wallet.Name, StringComparison.InvariantCultureIgnoreCase));
            if (node != null)
            {
                return node.Wallet.DefaultAccount;
            }

            if (GENESIS.Equals(name, StringComparison.InvariantCultureIgnoreCase))
            {
                return chain.ConsensusNodes
                    .Select(n => n.Wallet.Accounts.Single(a => a.IsMultiSigContract()))
                    .FirstOrDefault();
            }

            return null;
        }

        public Task<BigInteger> ShowBalance(ExpressChain chain, ExpressWalletAccount account, string asset)
        {
            var uri = chain.GetUri();
            var nep5client = new Nep5API(new RpcClient(uri.ToString()));

            var assetHash = NodeUtility.GetAssetId(asset);

            var result = nep5client.BalanceOf(assetHash, account.ScriptHash.ToScriptHash());
            return Task.FromResult(result);
        }

        public Task<RpcTransaction> ShowTransaction(ExpressChain chain, string txHash)
        {
            var uri = chain.GetUri();
            var rpcClient = new RpcClient(uri.ToString());

            var result = rpcClient.GetRawTransaction(txHash);
            return Task.FromResult(result);
        }

        public async Task<IReadOnlyList<ExpressStorage>> GetStorages(ExpressChain chain, string scriptHash)
        {
            var uri = chain.GetUri();
            var rpcClient = new RpcClient(uri.ToString());

            var json = await Task.Run(() => rpcClient.RpcSend("expressgetcontractstorage", scriptHash))
                .ConfigureAwait(false);

            if (json != null && json is Neo.IO.Json.JArray array)
            {
                var storages = new List<ExpressStorage>(array.Count);
                foreach (var s in array)
                {
                    var storage = new ExpressStorage()
                    {
                        Key = s["key"].AsString(),
                        Value = s["value"].AsString(),
                        Constant = s["constant"].AsBoolean()
                    };
                    storages.Add(storage);
                }
                return storages;
            }

            return new List<ExpressStorage>(0);
        }
    }
}
