using System;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;

namespace NeoExpress.Commands
{
    [Command("run", Description = "Run Neo-Express instance node")]
    class RunCommand
    {
        readonly IChainManager chainManager;
        readonly INodeManager nodeManager;

        public RunCommand(IChainManager chainManager, INodeManager nodeManager)
        {
            this.chainManager = chainManager;
            this.nodeManager = nodeManager;
        }

        [Argument(0, Description = "Index of node to run")]
        internal int NodeIndex { get; } = 0;

        [Option(Description = "Path to neo-express data file")]
        internal string Input { get; } = string.Empty;

        [Option(Description = "Time between blocks")]
        internal uint SecondsPerBlock { get; }

        [Option(Description = "Discard blockchain changes on shutdown")]
        internal bool Discard { get; } = false;

        [Option(Description = "Enable contract execution tracing")]
        internal bool Trace { get; } = false;

        internal async Task ExecuteAsync(IConsole console, CancellationToken token)
        {
            var (chain, _) = chainManager.Load(Input);

            if (NodeIndex < 0 || NodeIndex >= chain.ConsensusNodes.Count)
            {
                throw new Exception("Invalid node index");
            }

            var node = chain.ConsensusNodes[NodeIndex];
            var folder = nodeManager.GetNodePath(node);
            await console.Out.WriteLineAsync(folder).ConfigureAwait(false);

            using IStore store = GetStore(folder);
            await nodeManager.RunAsync(store, chain, node, SecondsPerBlock, Trace, console, token).ConfigureAwait(false);
        }

        internal async Task<int> OnExecuteAsync(IConsole console, CancellationToken token)
        {
            try
            {
                await ExecuteAsync(console, token);
                return 0;
            }
            catch (Exception ex)
            {
                await console.Error.WriteLineAsync(ex.Message);
                return 1;
            }
        }

        IStore GetStore(string folder)
        {
            if (Discard)
            {
                try
                {
                    var rocksDbStore = RocksDbStore.OpenReadOnly(folder);
                    return new CheckpointStore(rocksDbStore);
                }
                catch
                {
                    return new CheckpointStore(NullReadOnlyStore.Instance);
                }
            }
            else
            {
                return RocksDbStore.Open(folder);
            }
        }
    }
}
