using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;


namespace NeoExpress.Commands
{
    partial class CheckpointCommand
    {
        [Command("run", Description = "Run a neo-express checkpoint (discarding changes on shutdown)")]
        class Run
        {
            [Argument(0, "Checkpoint file name")]
            [Required]
            string Name { get; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            [Option(Description = "Time between blocks")]
            uint SecondsPerBlock { get; }

            [Option(Description = "Enable contract execution tracing")]
            bool Trace { get; } = false;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    // var blockchainOperations = new BlockchainOperations();
                    // var filename = blockchainOperations.ResolveCheckpointFileName(Name);
                    // if (!File.Exists(filename))
                    // {
                    //     throw new Exception($"Checkpoint {filename} couldn't be found");
                    // }

                    // var (chain, _) = Program.LoadExpressChain(Input);
                    // using var cts = new CancellationTokenSource();
                    // console.CancelKeyPress += (sender, args) => cts.Cancel();

                    // await blockchainOperations.RunCheckpointAsync(chain,
                    //                                               filename,
                    //                                               SecondsPerBlock,
                    //                                               Trace,
                    //                                               console.Out,
                    //                                               cts.Token)
                    //         .ConfigureAwait(false);

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
}
