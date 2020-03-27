using System;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Neo3;

namespace nxp3.Commands
{
    [Command("run")]
    class RunCommand
    {
        [Argument(0)]
        private int NodeIndex { get; } = 0;

        [Option]
        private string Input { get; } = string.Empty;

        [Option]
        private uint SecondsPerBlock { get; }

        [Option]
        private bool Reset { get; } = false;

        [Option]
        private bool Discard { get; } = false;

        private async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
        {
            try
            {
                var (chain, _) = Program.LoadExpressChain(Input);

                if (NodeIndex < 0 || NodeIndex >= chain.ConsensusNodes.Count)
                {
                    throw new Exception("Invalid node index");
                }

                using var cts = new CancellationTokenSource();
                console.CancelKeyPress += (sender, args) => cts.Cancel();
                var blockchainOperations = new BlockchainOperations();

                if (Discard)
                {
                    await blockchainOperations.RunCheckpointAsync(chain,
                                                                NodeIndex,
                                                                SecondsPerBlock,
                                                                console.Out,
                                                                cts.Token)
                        .ConfigureAwait(false);
                }
                else
                {
                    await blockchainOperations.RunBlockchainAsync(chain,
                                                                NodeIndex,
                                                                SecondsPerBlock,
                                                                Reset,
                                                                console.Out,
                                                                cts.Token)
                        .ConfigureAwait(false);
                }
                return 0;
            }
            catch (Exception ex)
            {
                console.WriteLine(ex.Message);
                app.ShowHelp();
                return 1;
            }
        }
     }
}
