using McMaster.Extensions.CommandLineUtils;
using System.ComponentModel.DataAnnotations;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Threading;

namespace NeoExpress.Neo2.Commands
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
                string checkpointTempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                try
                {
                    var filename = ValidateCheckpointFileName(Name);
                    var (chain, _) = Program.LoadExpressChain(Input);

                    if (chain.ConsensusNodes.Count > 1)
                    {
                        throw new Exception("Checkpoint run is only supported on single node express instances");
                    }

                    ZipFile.ExtractToDirectory(filename, checkpointTempPath);

                    using (var cts = new CancellationTokenSource())
                    {
                        console.CancelKeyPress += (sender, args) => cts.Cancel();

                        var blockchainOperations = new BlockchainOperations();
                        await blockchainOperations.RunCheckpointAsync(checkpointTempPath, chain, SecondsPerBlock,
                                                                      console.Out, cts.Token)
                            .ConfigureAwait(false);
                    }
                    return 0;
                }
                catch (Exception ex)
                {
                    console.WriteError(ex.Message);
                    app.ShowHelp();
                    return 1;
                }
                finally
                {
                    Directory.Delete(checkpointTempPath, true);
                }
            }
        }
    }
}
