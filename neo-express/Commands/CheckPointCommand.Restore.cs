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

                    if (!Force)
                    {
                        throw new Exception("You must specify force to restore a blockchain checkpoint.");
                    }

                    var (devChain, _) = DevChain.Load(Input);

                    if (devChain.ConsensusNodes.Count > 1)
                    {
                        throw new Exception("Checkpoint create and restore is only supported on single node express instances");
                    }

                    string checkpointTempPath = Path.Combine(
                        Path.GetTempPath(), Path.GetRandomFileName());

                    ZipFile.ExtractToDirectory(filename, checkpointTempPath);

                    var consensusNode = devChain.ConsensusNodes[0];
                    var blockchainPath = consensusNode.BlockchainPath;

                    Directory.Delete(blockchainPath, true);
                    Directory.Move(checkpointTempPath, blockchainPath);

                    console.WriteLine($"Checkpoint {Name} sucessfully restored");
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
