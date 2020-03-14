using McMaster.Extensions.CommandLineUtils;
using System.ComponentModel.DataAnnotations;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Threading;

namespace NeoExpress.Commands
{
    internal partial class CheckPointCommand
    {
        [Command("run")]
        class Run
        {
            [Argument(0)]
            [Required]
            private string Name { get; } = string.Empty;

            [Option]
            private string Input { get; } = string.Empty;

            [Option]
            private uint SecondsPerBlock { get; }

            private async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);
                    using var cts = new CancellationTokenSource();
                    console.CancelKeyPress += (sender, args) => cts.Cancel();

                    var blockchainOperations = new NeoExpress.Neo2.BlockchainOperations();
                    await blockchainOperations.RunCheckpointAsync(chain,
                                                                  Name,
                                                                  SecondsPerBlock,
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
}
