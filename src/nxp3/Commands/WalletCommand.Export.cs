using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Abstractions.Models;
using NeoExpress.Abstractions;
using NeoExpress.Neo3;

namespace nxp3.Commands
{
    partial class WalletCommand
    {
        [Command("export")]
        class Export
        {
            [Argument(0)]
            [Required]
            private string Name { get; } = string.Empty;

            [Option]
            private string Input { get; } = string.Empty;

            [Option]
            private string Output { get; } = string.Empty;

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
                    if (wallet == null)
                    {
                        console.WriteLine($"{Name} privatenet wallet not found.");
                    }
                    else
                    {
                        var password = Prompt.GetPassword("Input password to use for exported wallet");
                        var blockchainOperations = new BlockchainOperations();
                        blockchainOperations.ExportWallet(wallet, output, password);
                        console.WriteLine($"{Name} privatenet wallet exported to {output}");
                    }

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
