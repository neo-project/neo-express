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
        readonly IBlockchainOperations blockchainOperations;

        public RunCommand(IBlockchainOperations blockchainOperations)
        {
            this.blockchainOperations = blockchainOperations;
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
            var (chain, _) = blockchainOperations.LoadChain(Input);

            if (NodeIndex < 0 || NodeIndex >= chain.ConsensusNodes.Count) throw new Exception("Invalid node index");

            var node = chain.ConsensusNodes[NodeIndex];
            var nodeRunner = blockchainOperations.GetNodeRunner(chain, SecondsPerBlock);
            using var store = blockchainOperations.GetNodeStore(node, Discard);
            await nodeRunner(store, node, Trace, console.Out, token).ConfigureAwait(false);
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
    }
}
