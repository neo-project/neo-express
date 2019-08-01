using McMaster.Extensions.CommandLineUtils;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.IO.Compression;

namespace Neo.Express.Commands
{
    partial class CheckPointCommand
    {
        [Command("restore")]
        class Restore
        {
            [Argument(0)]
            [Required]
            private string Name { get; }

            [Option]
            private string Input { get; }

            [Option]
            private bool Force { get; }

            private int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var filename = Name + ".neo-express";

                    if (!File.Exists(filename))
                    {
                        throw new Exception($"Checkpoint {Name} couldn't be found");
                    }

                    var (devChain, _) = DevChain.Load(Input);

                    if (devChain.ConsensusNodes.Count > 1)
                    {
                        throw new Exception("Checkpoint restore is only supported on single node express instances");
                    }

                    var blockchainPath = devChain.ConsensusNodes[0].BlockchainPath;

                    if (!Force && Directory.Exists(blockchainPath))
                    {
                        throw new Exception("You must specify force to restore a checkpoint to an existing blockchain.");
                    }

                    string checkpointTempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                    ZipFile.ExtractToDirectory(filename, checkpointTempPath);

                    if (Directory.Exists(blockchainPath))
                    {
                        Directory.Delete(blockchainPath, true);
                    }
                    Directory.Move(checkpointTempPath, blockchainPath);

                    console.WriteLine($"Checkpoint {Name} sucessfully restored");
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
