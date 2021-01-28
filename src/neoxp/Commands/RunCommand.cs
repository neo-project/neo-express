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
        readonly IExpressChainManagerFactory chainManagerFactory;

        public RunCommand(IExpressChainManagerFactory chainManagerFactory)
        {
            this.chainManagerFactory = chainManagerFactory;
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
            var (chainManager, _) = chainManagerFactory.LoadChain(Input);
            var chain = chainManager.Chain;

            if (NodeIndex < 0 || NodeIndex >= chain.ConsensusNodes.Count) throw new Exception("Invalid node index");

            var node = chain.ConsensusNodes[NodeIndex];
            using var store = chainManager.GetNodeStore(node, Discard);
            await chainManager.RunAsync(store, node, SecondsPerBlock, Trace, console.Out, token);
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
