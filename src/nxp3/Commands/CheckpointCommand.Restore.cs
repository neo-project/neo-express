using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Neo3;

namespace nxp3.Commands
{
    partial class CheckpointCommand
    {
        [Command("restore")]
        class Restore
        {
            [Argument(0)]
            [Required]
            string Name { get; } = string.Empty;

            [Option]
            string Input { get; } = string.Empty;

            [Option]
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
                    console.WriteLine(ex.Message);
                    return 1;
                }
            }
        }
    }
}
