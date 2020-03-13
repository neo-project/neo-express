using McMaster.Extensions.CommandLineUtils;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.IO.Compression;

namespace NeoExpress.Neo2.Commands
{
    partial class CheckPointCommand
    {
        [Command("restore")]
        class Restore
        {
            [Argument(0)]
            [Required]
            private string Name { get; } = string.Empty;

            [Option]
            private string Input { get; } = string.Empty;

            [Option]
            private bool Force { get; }

            private int OnExecute(CommandLineApplication app, IConsole console)
            {
                string checkpointTempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

                try
                {
                    var filename = ValidateCheckpointFileName(Name);
                    var (chain, _) = Program.LoadExpressChain(Input);

                    if (chain.ConsensusNodes.Count > 1)
                    {
                        throw new Exception("Checkpoint restore is only supported on single node express instances");
                    }

                    var node = chain.ConsensusNodes[0];
                    var blockchainPath = node.GetBlockchainPath();
                    if (!Force && Directory.Exists(blockchainPath))
                    {
                        throw new Exception("You must specify force to restore a checkpoint to an existing blockchain.");
                    }

                    ZipFile.ExtractToDirectory(filename, checkpointTempPath);

                    if (Directory.Exists(blockchainPath))
                    {
                        Directory.Delete(blockchainPath, true);
                    }

                    var blockchainOperations = new BlockchainOperations();
                    blockchainOperations.RestoreCheckpoint(chain, blockchainPath, checkpointTempPath);

                    console.WriteLine($"Checkpoint {Name} sucessfully restored");
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
                    if (Directory.Exists(checkpointTempPath))
                    {
                        Directory.Delete(checkpointTempPath, true);
                    }
                }
            }
        }
    }
}
