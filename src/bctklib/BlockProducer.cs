// Copyright (C) 2015-2026 The Neo Project.
//
// BlockProducer.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Cryptography;
using Neo.Extensions;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;

namespace Neo.BlockchainToolkit
{
    // Dev-chain block production shared by the Neo-Express and Neo-WorkNet fast forward
    // implementations: build empty blocks signed by the chain's consensus keys and either
    // hand them to a submit callback (running node) or persist them directly to a store.
    public static class BlockProducer
    {
        public const uint MaxFastForwardCount = 100_000;

        public static Block CreateSignedBlock(Header prevHeader, IReadOnlyList<KeyPair> keyPairs, uint network, Transaction[]? transactions = null, ulong timestamp = 0)
        {
            transactions ??= Array.Empty<Transaction>();

            var blockHeight = prevHeader.Index + 1;

            // dBFT assigns each block a random nonce. Mirror that here so the block
            // nonce contribution to System.Runtime.GetRandom varies per block instead
            // of being a constant 0, which otherwise makes on-chain randomness behave
            // differently from a real network.
            Span<byte> nonceBuffer = stackalloc byte[sizeof(ulong)];
            System.Security.Cryptography.RandomNumberGenerator.Fill(nonceBuffer);
            var nonce = BitConverter.ToUInt64(nonceBuffer);

            var block = new Block
            {
                Header = new Header
                {
                    Version = 0,
                    Nonce = nonce,
                    PrevHash = prevHeader.Hash,
                    MerkleRoot = MerkleTree.ComputeRoot(transactions.Select(t => t.Hash).ToArray()),
                    Timestamp = timestamp > prevHeader.Timestamp
                        ? timestamp
                        : Math.Max(DateTime.UtcNow.ToTimestampMS(), prevHeader.Timestamp + 1),
                    Index = blockHeight,
                    PrimaryIndex = 0,
                    NextConsensus = prevHeader.NextConsensus,
                    Witness = Witness.Empty
                },
                Transactions = transactions
            };

            // generate the block header witness. Logic lifted from ConsensusContext.CreateBlock
            var m = keyPairs.Count - (keyPairs.Count - 1) / 3;
            var contract = Contract.CreateMultiSigContract(m, keyPairs.Select(k => k.PublicKey).ToList());
            var signingContext = new ContractParametersContext(null, new BlockScriptHashes(prevHeader.NextConsensus), network);
            for (int i = 0; i < keyPairs.Count; i++)
            {
                var signature = block.Header.Sign(keyPairs[i], network);
                signingContext.AddSignature(contract, keyPairs[i].PublicKey, signature);
                if (signingContext.Completed)
                    break;
            }
            if (!signingContext.Completed)
                throw new Exception("block signing incomplete");
            block.Header.Witness = signingContext.GetWitnesses()[0];

            return block;
        }

        public static async Task FastForwardAsync(Header prevHeader, uint blockCount, TimeSpan timestampDelta, KeyPair[] keyPairs, uint network, Func<Block, Task> submitBlockAsync)
        {
            if (timestampDelta.TotalSeconds < 0)
                throw new ArgumentException($"Negative {nameof(timestampDelta)} not supported");
            if (blockCount == 0)
                return;

            var timestamp = Math.Max(DateTime.UtcNow.ToTimestampMS(), prevHeader.Timestamp + 1);
            var delta = (ulong)timestampDelta.TotalMilliseconds;

            if (blockCount == 1)
            {
                var block = CreateSignedBlock(
                    prevHeader, keyPairs, network, timestamp: timestamp + delta);
                await submitBlockAsync(block).ConfigureAwait(false);
            }
            else
            {
                var period = delta / (blockCount - 1);
                for (int i = 0; i < blockCount; i++)
                {
                    var block = CreateSignedBlock(
                        prevHeader, keyPairs, network, timestamp: timestamp);
                    await submitBlockAsync(block).ConfigureAwait(false);
                    prevHeader = block.Header;
                    timestamp += period;
                }
            }
        }

        // Fast forward a chain that is not running by persisting the signed empty blocks
        // directly to its store, using the same native OnPersist/PostPersist scripts the
        // blockchain runs for every block.
        public static void FastForward(IStore store, uint blockCount, TimeSpan timestampDelta, KeyPair[] keyPairs, ProtocolSettings settings)
        {
            Header prevHeader;
            using (var snapshot = new StoreCache(store.GetSnapshot()))
            {
                var prevHash = NativeContract.Ledger.CurrentHash(snapshot);
                prevHeader = NativeContract.Ledger.GetHeader(snapshot, prevHash)
                    ?? throw new InvalidOperationException($"Block header {prevHash} is missing");
            }

            FastForwardAsync(prevHeader, blockCount, timestampDelta, keyPairs, settings.Network,
                block =>
                {
                    Persist(store, block, settings);
                    return Task.CompletedTask;
                }).GetAwaiter().GetResult();
        }

        // replicated logic from Blockchain.Persist / Extensions.EnsureLedgerInitialized
        static void Persist(IStore store, Block block, ProtocolSettings settings)
        {
            using var snapshot = new StoreCache(store.GetSnapshot());

            using (var engine = ApplicationEngine.Create(TriggerType.OnPersist, null, snapshot, block, settings, 0L))
            {
                using var scriptBuilder = new ScriptBuilder();
                scriptBuilder.EmitSysCall(ApplicationEngine.System_Contract_NativeOnPersist);
                engine.LoadScript(scriptBuilder.ToArray());
                if (engine.Execute() != VMState.HALT)
                    throw new InvalidOperationException($"NativeOnPersist failed for block {block.Index}", engine.FaultException);
            }

            using (var engine = ApplicationEngine.Create(TriggerType.PostPersist, null, snapshot, block, settings, 0L))
            {
                using var scriptBuilder = new ScriptBuilder();
                scriptBuilder.EmitSysCall(ApplicationEngine.System_Contract_NativePostPersist);
                engine.LoadScript(scriptBuilder.ToArray());
                if (engine.Execute() != VMState.HALT)
                    throw new InvalidOperationException($"NativePostPersist failed for block {block.Index}", engine.FaultException);
            }

            snapshot.Commit();
        }

        public static void ValidateCount(uint count)
        {
            if (count > MaxFastForwardCount)
                throw new Exception($"Cannot mint more than {MaxFastForwardCount} blocks at once");
        }

        public static TimeSpan ParseTimestampDelta(string timestampDelta)
        {
            var delta = string.IsNullOrEmpty(timestampDelta)
                ? TimeSpan.Zero
                : ulong.TryParse(timestampDelta, out var @ulong)
                    ? TimeSpan.FromSeconds(@ulong)
                    : TimeSpan.TryParse(timestampDelta, out var timeSpan)
                        ? timeSpan
                        : throw new Exception($"Could not parse timestamp delta {timestampDelta}");

            if (delta < TimeSpan.Zero)
                throw new Exception($"Timestamp delta {timestampDelta} cannot be negative");

            return delta;
        }

        // Need an IVerifiable.GetScriptHashesForVerifying implementation that doesn't
        // depend on the DataCache snapshot parameter in order to create a
        // ContractParametersContext without direct access to node data.
        private class BlockScriptHashes : IVerifiable
        {
            private readonly UInt160[] hashes;

            public BlockScriptHashes(UInt160 scriptHash)
            {
                hashes = new[] { scriptHash };
            }

            public UInt160[] GetScriptHashesForVerifying(DataCache snapshot) => hashes;

            Witness[] IVerifiable.Witnesses
            {
                get => throw new NotImplementedException();
                set => throw new NotImplementedException();
            }

            int ISerializable.Size => throw new NotImplementedException();

            void ISerializable.Serialize(BinaryWriter writer) => throw new NotImplementedException();

            void IVerifiable.SerializeUnsigned(BinaryWriter writer) => throw new NotImplementedException();

            void ISerializable.Deserialize(ref MemoryReader reader) => throw new NotImplementedException();

            void IVerifiable.DeserializeUnsigned(ref MemoryReader reader) => throw new NotImplementedException();
        }
    }
}
