using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Neo3;

namespace nxp3.Commands
{
    partial class CheckpointCommand
    {
        [Command("run")]
        class Run
        {
            [Argument(0)]
            [Required]
            string Name { get; } = string.Empty;

            [Option]
            string Input { get; } = string.Empty;

            [Option]
            uint SecondsPerBlock { get; }

            [Option]
            bool Trace { get; } = false;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var blockchainOperations = new BlockchainOperations();
                    var filename = blockchainOperations.ResolveCheckpointFileName(Name);
                    if (!File.Exists(filename))
                    {
                        throw new Exception($"Checkpoint {filename} couldn't be found");
                    }

                    var (chain, _) = Program.LoadExpressChain(Input);
                    using var cts = new CancellationTokenSource();
                    console.CancelKeyPress += (sender, args) => cts.Cancel();

                    await blockchainOperations.RunCheckpointAsync(chain,
                                                                  filename,
                                                                  SecondsPerBlock,
                                                                  Trace,
                                                                  console.Out,
                                                                  cts.Token)
                            .ConfigureAwait(false);

                    return 0;
                }
                catch (Exception ex)
                {
                    console.Error.WriteLine(ex.Message);
                    return 1;
                }
            }
        }
    }
}
