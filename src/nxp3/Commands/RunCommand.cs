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
        int NodeIndex { get; } = 0;

        [Option]
        string Input { get; } = string.Empty;

        [Option]
        uint SecondsPerBlock { get; }

        [Option]
        bool Discard { get; } = false;

        [Option]
        bool Trace { get; } = false;

        internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
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

                await blockchainOperations.RunBlockchainAsync(chain,
                                                            NodeIndex,
                                                            SecondsPerBlock,
                                                            Discard,
                                                            Trace,
                                                            console.Out,
                                                            cts.Token)
                    .ConfigureAwait(false);

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
