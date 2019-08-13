using McMaster.Extensions.CommandLineUtils;
using System.ComponentModel.DataAnnotations;
using System;
using System.IO;
using System.IO.Compression;

namespace Neo.Express.Commands
{
    internal partial class CheckPointCommand
    {
        [Command("run")]
        class Run
        {
            [Argument(0)]
            [Required]
            private string Name { get; }

            [Option]
            private string Input { get; }

            [Option]
            private uint SecondsPerBlock { get; }

            private int OnExecute(CommandLineApplication app, IConsole console)
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

                    var cts = Program.GetBackend().RunCheckpoint(
                        checkpointTempPath, chain, SecondsPerBlock,
                        s => console.WriteLine(s));

                    console.CancelKeyPress += (sender, args) => cts.Cancel();
                    cts.Token.WaitHandle.WaitOne();

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
