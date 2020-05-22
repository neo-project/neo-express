using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    [Command("create")]
    internal class CreateCommand
    {
        [AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false)]
        private class ValidNodeCountAttribute : ValidationAttribute
        {
            public ValidNodeCountAttribute() : base("The value for {0} must be 1, 4 or 7")
            {
            }

            protected override ValidationResult IsValid(object value, ValidationContext context)
            {
                if (value == null || (value is string str && str != "1" && str != "4" && str != "7"))
                {
                    return new ValidationResult(FormatErrorMessage(context.DisplayName));
                }

                return ValidationResult.Success;
            }
        }

        [ValidNodeCount]
        [Option]
        private int Count { get; }

        [Option]
        private string Output { get; } = string.Empty;

        [Option]
        private bool Force { get; }

        [Option]
        private uint PreloadGas { get; }


        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            try
            {
                var output = Program.GetDefaultFilename(Output);
                if (File.Exists(output) && !Force)
                {
                    throw new Exception("You must specify --force to overwrite an existing file");
                }

                var count = (Count == 0 ? 1 : Count);
                if (PreloadGas > 0 && count != 1)
                {
                    throw new Exception("you can only specify --preload-gas for a single node neo-express blockchain");
                }

                var chain = BlockchainOperations.CreateBlockchain(count);
                chain.Save(output);

                console.WriteLine($"Created {count} node privatenet at {output}");
                console.WriteWarning("    Note: The private keys for the accounts in this file are are *not* encrypted.");
                console.WriteWarning("          Do not use these accounts on MainNet or in any other system where security is a concern.");

                if (PreloadGas > 0)
                {
                    var node = chain.ConsensusNodes[0];
                    var folder = node.GetBlockchainPath();

                    if (!Directory.Exists(folder))
                    {
                        Directory.CreateDirectory(folder);
                    }

                    using var cts = new CancellationTokenSource();
                    console.CancelKeyPress += (sender, args) => cts.Cancel();
                    BlockchainOperations.PreloadGas(folder, chain, 0, PreloadGas, console.Out, cts.Token);
                }

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
