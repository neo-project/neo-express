using System;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit.Models;

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

        internal Task<int> OnExecuteAsync(CommandLineApplication app) => app.ExecuteAsync(this.ExecuteAsync);

        internal async Task ExecuteAsync(IConsole console)
        {
            if (NodeIndex.HasValue && All)
            {
                throw new InvalidOperationException("Only one of NodeIndex or --all can be specified");
            }

            if (All)
            {
                var tasks = new Task[chain.ConsensusNodes.Count];
                for (int i = 0; i < chain.ConsensusNodes.Count; i++)
                {
                    tasks[i] = StopNodeAsync(chain.ConsensusNodes[i]);
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
                await console.Out.WriteLineAsync($"all nodes stopped").ConfigureAwait(false);
            }
            else
            {
                var nodeIndex = NodeIndex.HasValue
                    ? NodeIndex.Value
                    : chain.ConsensusNodes.Count == 1
                        ? 0
                        : throw new ArgumentException("node index or --all must be specified when resetting a multi-node chain");
                if (nodeIndex < 0 || nodeIndex >= chain.ConsensusNodes.Count) { throw new ArgumentException("Invalid node index"); }

                var wasRunning = await StopNodeAsync(chain.ConsensusNodes[nodeIndex]).ConfigureAwait(false);
                await console.Out.WriteLineAsync($"node {nodeIndex} {(wasRunning ? "stopped" : "was not running")}").ConfigureAwait(false);
            }

            static async Task<bool> StopNodeAsync(ExpressConsensusNode node)
            {
                if (!node.IsRunning()) return false;

                var rpcClient = new Neo.Network.RPC.RpcClient(new Uri($"http://localhost:{node.RpcPort}"));
                var json = await rpcClient.RpcSendAsync("expressshutdown").ConfigureAwait(false);
                var processId = int.Parse(json["process-id"].AsString());
                var process = System.Diagnostics.Process.GetProcessById(processId);
                await process.WaitForExitAsync().ConfigureAwait(false);
                return true;
            }
        }
    }
}
