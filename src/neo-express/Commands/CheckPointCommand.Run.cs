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
                try
                {
                    var filename = ValidateCheckpointFileName(Name);

                    var devChain = DevChain.Initialize(Input, SecondsPerBlock);

                    if (devChain.ConsensusNodes.Count > 1)
                    {
                        throw new Exception("Checkpoint run is only supported on single node express instances");
                    }

                    var consensusNode = devChain.ConsensusNodes[0];
                    ValidateCheckpointAddress(filename, consensusNode);

                    string checkpointTempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                    ZipFile.ExtractToDirectory(filename, checkpointTempPath);

                    var cts = RunCommand.Run(new CheckpointStore(checkpointTempPath), consensusNode, console);
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
            }
        }
    }
}
