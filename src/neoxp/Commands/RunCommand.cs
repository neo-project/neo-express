using System;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

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
        internal int NodeIndex { get; init; } = 0;

        [Option(Description = "Path to neo-express data file")]
        internal string Input { get; init; } = string.Empty;

        [Option(Description = "Time between blocks")]
        internal uint? SecondsPerBlock { get; }

        [Option(Description = "Discard blockchain changes on shutdown")]
        internal bool Discard { get; init; } = false;

        [Option(Description = "Enable contract execution tracing")]
        internal bool Trace { get; init; } = false;

        internal async Task ExecuteAsync(IConsole console, CancellationToken token)
        {
            var (chainManager, _) = chainManagerFactory.LoadChain(Input, SecondsPerBlock);
            var chain = chainManager.Chain;

            if (NodeIndex < 0 || NodeIndex >= chain.ConsensusNodes.Count) throw new Exception("Invalid node index");

            var node = chain.ConsensusNodes[NodeIndex];
            using var storageProvider = chainManager.GetNodeStorageProvider(node, Discard);
            await chainManager.RunAsync(storageProvider, node, Trace, console.Out, token);
        }

        internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console, CancellationToken token)
        {
            try
            {
                await ExecuteAsync(console, token);
                return 0;
            }
            catch (Exception ex)
            {
                app.WriteException(ex);
                return 1;
            }
        }
    }
}
