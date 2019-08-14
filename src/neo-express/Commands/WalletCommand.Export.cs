using McMaster.Extensions.CommandLineUtils;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;

namespace NeoExpress.Commands
{
    internal partial class WalletCommand
    {
        [Command("export")]
        private class Export
        {
            [Argument(0)]
            [Required]
            private string Name { get; }

            [Option]
            private string Input { get; }

            [Option]
            private string Output { get; }

            [Option]
            private bool Force { get; }

            private int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var output = string.IsNullOrEmpty(Output)
                       ? Path.Combine(Directory.GetCurrentDirectory(), $"{Name}.wallet.json")
                       : Output;

                    if (File.Exists(output))
                    {
                        if (Force)
                        {
                            File.Delete(output);
                        }
                        else
                        {
                            throw new Exception("You must specify force to overwrite an exported wallet.");
                        }
                    }

                    var (chain, _) = Program.LoadExpressChain(Input);
                    var wallet = chain.GetWallet(Name);
                    if (wallet == default)
                    {
                        console.WriteLine($"{Name} privatenet wallet not found.");
                    }
                    else
                    {
                        var password = Prompt.GetPassword("Input password to use for exported wallet");
                        Program.GetBackend().ExportWallet(wallet, output, password);
                        console.WriteLine($"{Name} privatenet wallet exported to {output}");
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
}
