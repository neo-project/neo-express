using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;

namespace NeoExpress.Commands
{
    [Command("resync", Description = "Resync the express chain")]
    class ResyncCommand
    {
        readonly ExpressChainManagerFactory chainManagerFactory;

        public ResyncCommand(ExpressChainManagerFactory chainManagerFactory)
        {
            this.chainManagerFactory = chainManagerFactory;
        }

        [Option(Description = "Path to neo-express data file")]
        internal string Input { get; init; } = string.Empty;

        internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
        {
            try
            {
                var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                if (chainManager.IsRunning())
                {
                    throw new Exception("Cannot run resync command while blockchain is running");
                }

                IReadOnlyList<Block> blocks;
                {
                    var provider = chainManager.GetNodeStorageProvider(chainManager.Chain.ConsensusNodes[0], true);
                    using var _ = provider as IDisposable;
                    using var store = provider.GetStore(null);
                    using var snapshot = new Neo.Persistence.SnapshotCache(store);
                    blocks = GetBlocks(snapshot).ToList();
                }

                foreach (var node in chainManager.Chain.ConsensusNodes)
                {
                    chainManager.ResetNode(node, true);
                }

                using (var offlineNode = (Node.OfflineNode)chainManager.GetExpressNode())
                {
                    foreach (var block in blocks)
                    {
                        await offlineNode.RelayBlockAsync(block).ConfigureAwait(false);
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                app.WriteException(ex, showInnerExceptions: true);
                return 1;
            }
        }

        static IEnumerable<Block> GetBlocks(DataCache snapshot)
        {
            foreach (var (index, hash) in GetBlockHashes(snapshot))
            {
                var block = NativeContract.Ledger.GetTrimmedBlock(snapshot, hash);
                if (block.Index == 0) continue;
                var txs = block.Hashes.Select(h => GetTransaction(snapshot, h));

                yield return new Block
                {
                    Header = block.Header,
                    Transactions = txs.ToArray(),
                };
            }

            static IEnumerable<(uint index, UInt256 hash)> GetBlockHashes(DataCache snapshot)
            {
                const byte Prefix_BlockHash = 9;
                var key = new KeyBuilder(NativeContract.Ledger.Id, Prefix_BlockHash);
                return snapshot.Find(key.ToArray()).Select(record => {
                    var index = BinaryPrimitives.ReadUInt32BigEndian(record.Key.Key.Span);
                    var hash = new UInt256(record.Value.Value.Span);
                    return (index, hash);
                });
            }

            static Transaction GetTransaction(DataCache snapshot, UInt256 hash)
            {
                const byte Prefix_Transaction = 11;
                var key = new KeyBuilder(NativeContract.Ledger.Id, Prefix_Transaction)
                    .Add(hash.ToArray());

                var item = snapshot.TryGet(key);
                if (item is null) throw new Exception("Invalid transaction hash");
                var txState = item.GetInteroperable<TransactionState>();
                return txState.Transaction;
            }
        }
    }
}
