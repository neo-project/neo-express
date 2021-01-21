using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Neo3;

namespace nxp3.Commands
{
    partial class CheckpointCommand
    {
        [Command("restore", Description = "Restore a neo-express checkpoint")]
        class Restore
        {
            [Argument(0, "Checkpoint file name")]
            [Required]
            string Name { get; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            [Option(Description = "Overwrite existing data")]
            bool Force { get; }

            internal int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);

                    var blockchainOperations = new BlockchainOperations();
                    var filename = blockchainOperations.ResolveCheckpointFileName(Name);
                    if (!File.Exists(filename))
                    {
                        throw new Exception($"Checkpoint {filename} couldn't be found");
                    }

                    blockchainOperations.RestoreCheckpoint(chain, filename, Force);

                    console.WriteLine($"Checkpoint {Name} successfully restored");
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
