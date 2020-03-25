using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Abstractions;
using NeoExpress.Neo2;

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
                if (File.Exists(output))
                {
                    if (Force)
                    {
                        File.Delete(output);
                    }
                    else 
                    {
                        throw new Exception("You must specify --force to overwrite an existing file");
                    }
                }

                var count = (Count == 0 ? 1 : Count);

                var blockchainOperations = new BlockchainOperations();
                using var cts = new CancellationTokenSource();
                console.CancelKeyPress += (sender, args) => cts.Cancel();
                var chain = blockchainOperations.CreateBlockchain(new FileInfo(output), count, PreloadGas, Console.Out, cts.Token);
                chain.Save(output);

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
