using System.Numerics;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;

using NeoArray = Neo.VM.Types.Array;
using ByteString = Neo.VM.Types.ByteString;
using NeoInt = Neo.VM.Types.Integer;
using NeoStruct = Neo.VM.Types.Struct;
using Neo.Cryptography;

namespace NeoWorkNet;

class Program
{
    const byte Prefix_Block = 5;
    const byte Prefix_BlockHash = 9;
    const byte Prefix_Candidate = 33;
    const byte Prefix_Committee = 14;
    const byte Prefix_CurrentBlock = 12;

    static async Task Main(string[] args)
    {
        var url = Constants.TESTNET_RPC_ENDPOINTS.First();
        using var stateStore = await StateServiceStore.CreateAsync(url, 5000).ConfigureAwait(false);
        
        var consensusWallet = new ToolkitWallet("consensus", stateStore.Settings);
        var consensusAccount = consensusWallet.CreateAccount();
        var consensusContract = Contract.CreateMultiSigContract(1, consensusWallet.GetAccounts().Select(a => a.GetKey().PublicKey).ToArray());


        using var trackingStore = new MemoryTrackingStore(stateStore);
        CreateFork(trackingStore, consensusAccount);
    }

    static void CreateFork(IStore store, params WalletAccount[] consensusAccounts)
    {
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
        foreach (var candidate in snapshot.Find(candidateKeyBuilder.ToArray()))
        {
            snapshot.Delete(candidate.Key);
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
