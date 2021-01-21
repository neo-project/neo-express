using System;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;


namespace NeoExpress.Commands
{
    [Command("run", Description = "Run Neo-Express instance node")]
    class RunCommand
    {
        [Argument(0, Description = "Index of node to run")]
        int NodeIndex { get; } = 0;

        [Option(Description = "Path to neo-express data file")]
        string Input { get; } = string.Empty;

        [Option(Description = "Time between blocks")]
        uint SecondsPerBlock { get; }

        [Option(Description = "Discard blockchain changes on shutdown")]
        bool Discard { get; } = false;

        [Option(Description = "Enable contract execution tracing")]
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
                await console.Error.WriteLineAsync(ex.Message);
                return 1;
            }
        }
    }
}
