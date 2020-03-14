using McMaster.Extensions.CommandLineUtils;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.IO.Compression;

namespace NeoExpress.Commands
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
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);

                    var blockchainOperations = new NeoExpress.Neo2.BlockchainOperations();
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
                    console.WriteError(ex.Message);
                    app.ShowHelp();
                    return 1;
                }
            }
        }
    }
}
