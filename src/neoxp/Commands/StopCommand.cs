using System;
using System.IO.Abstractions;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit.Models;

namespace NeoExpress.Commands
{
    [Command("stop", Description = "Stop Neo-Express instance node")]
    class StopCommand
    {
        readonly IFileSystem fileSystem;

        public StopCommand(IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        [Argument(0, Description = "Index of node to stop")]
        internal int? NodeIndex { get; }

        
        internal string Input { get; init; } = string.Empty;

        [Option(Description = "Stop all nodes")]
        internal bool All { get; }

        internal async Task ExecuteAsync(IConsole console)
        {
            if (NodeIndex.HasValue && All)
            {
                throw new InvalidOperationException("Only one of NodeIndex or --all can be specified");
            }

            var (chain, _) = fileSystem.LoadExpressChain(Input);

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
                        : throw new InvalidOperationException("node index or --all must be specified when resetting a multi-node chain");

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

        internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
        {
            try
            {
                await ExecuteAsync(console).ConfigureAwait(false);
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
