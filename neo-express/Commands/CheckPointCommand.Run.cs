using McMaster.Extensions.CommandLineUtils;
using System.ComponentModel.DataAnnotations;
using System;
using System.IO;
using System.IO.Compression;
using Neo.Express.Persistence;

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
                    var filename = Name + ".neo-express";

                    if (!File.Exists(filename))
                    {
                        throw new Exception($"Checkpoint {Name} couldn't be found");
                    }

                    var devChain = DevChain.Initialize(Input, SecondsPerBlock);

                    if (devChain.ConsensusNodes.Count > 1)
                    {
                        throw new Exception("Checkpoint run is only supported on single node express instances");
                    }

                    string checkpointTempPath = Path.Combine(
                        Path.GetTempPath(), 
                        $"neo-express-" + Path.GetRandomFileName());

                    ZipFile.ExtractToDirectory(filename, checkpointTempPath);

                    var cts = RunCommand.Run(new CheckpointStore(checkpointTempPath), devChain.ConsensusNodes[0], console);
                    console.CancelKeyPress += (sender, args) => cts.Cancel();
                    cts.Token.WaitHandle.WaitOne();
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
}
