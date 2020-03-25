using McMaster.Extensions.CommandLineUtils;
using System.IO;
using System;
using System.Threading;
using System.Threading.Tasks;
using NeoExpress.Neo2;

namespace NeoExpress.Commands
{
    [Command("run")]
    internal class RunCommand
    {
        [Argument(0)]
        private int NodeIndex { get; } = 0;

        [Option]
        private string Input { get; } = string.Empty;

        [Option]
        private uint SecondsPerBlock { get; }

        [Option]
        private bool Reset { get; }

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
                await blockchainOperations.RunBlockchainAsync(chain,
                                                              NodeIndex,
                                                              SecondsPerBlock,
                                                              Reset,
                                                              console.Out,
                                                              cts.Token)
                    .ConfigureAwait(false);

                return 0;
            }
            catch (Exception ex)
            {
                console.WriteError(ex.Message);
                app.ShowHelp();
                return 1;
            }
        }
    }
}