using McMaster.Extensions.CommandLineUtils;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.IO.Compression;
using System.Linq;

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
                    var filename = ValidateCheckpointFileName(Name);

                    var (devChain, _) = DevChain.Load(Input);

                    if (devChain.ConsensusNodes.Count > 1)
                    {
                        throw new Exception("Checkpoint restore is only supported on single node express instances");
                    }

                    var consensusNode = devChain.ConsensusNodes[0];

                    var blockchainPath = consensusNode.GetBlockchainPath();
                    if (!Force && Directory.Exists(blockchainPath))
                    {
                        throw new Exception("You must specify force to restore a checkpoint to an existing blockchain.");
                    }

                    ValidateCheckpointAddress(filename, consensusNode);

                    var checkpointTempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                    var addressFilePath = Path.Combine(checkpointTempPath, ADDRESS_FILENAME);

                    ZipFile.ExtractToDirectory(filename, checkpointTempPath);

                    if (File.Exists(addressFilePath))
                    {
                        File.Delete(addressFilePath);
                    }

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
