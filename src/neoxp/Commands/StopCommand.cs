using System;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    [Command("stop", Description = "Stop Neo-Express instance node")]
    class StopCommand
    {
        readonly IExpressChain chain;

        public StopCommand(IExpressChain chain)
        {
            this.chain = chain;
        }

        public StopCommand(CommandLineApplication app)
        {
            this.chain = app.GetExpressFile();
        }

        [Argument(0, Description = "Index of node to stop")]
        internal int? NodeIndex { get; }

        [Option(Description = "Stop all nodes")]
        internal bool All { get; }

        // internal async Task ExecuteAsync(IConsole console)
        // {
        //     if (NodeIndex.HasValue && All)
        //     {
        //         throw new InvalidOperationException("Only one of NodeIndex or --all can be specified");
        //     }

        //     var (chainManager, _) = chainManagerFactory.LoadChain(Input);
        //     var chain = chainManager.Chain;

        //     if (All)
        //     {
        //         var tasks = new Task[chain.ConsensusNodes.Count];
        //         for (int i = 0; i < chain.ConsensusNodes.Count; i++)
        //         {
        //             tasks[i] = chainManager.StopNodeAsync(chain.ConsensusNodes[i]);
        //         }
        //         await Task.WhenAll(tasks).ConfigureAwait(false);
        //         await console.Out.WriteLineAsync($"all nodes stopped").ConfigureAwait(false);
        //     }
        //     else
        //     {
        //         var nodeIndex = NodeIndex.HasValue
        //             ? NodeIndex.Value
        //             : chain.ConsensusNodes.Count == 1
        //                 ? 0
        //                 : throw new InvalidOperationException("node index or --all must be specified when resetting a multi-node chain");

        //         var wasRunning = await chainManager.StopNodeAsync(chain.ConsensusNodes[nodeIndex]).ConfigureAwait(false);
        //         await console.Out.WriteLineAsync($"node {nodeIndex} {(wasRunning ? "stopped" : "was not running")}").ConfigureAwait(false);
        //     }
        // }

        internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
        {
            try
            {
                // await ExecuteAsync(console).ConfigureAwait(false);
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
