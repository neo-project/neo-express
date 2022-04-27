using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit;
using NeoExpress.Models;

namespace NeoExpress.Commands
{
    partial class WalletCommand
    {
        [Command("export", Description = "Export neo-express wallet in NEP-6 format")]
        internal class Export
        {
            readonly IFileSystem fileSystem;

            public Export(IFileSystem fileSystem)
            {
                this.fileSystem = fileSystem;
            }

            [Argument(0, Description = "Wallet name")]
            [Required]
            internal string Name { get; init; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            [Option(Description = "NEP-6 wallet name (Defaults to Neo-Express name if unspecified)")]
            internal string Output { get; init; } = string.Empty;

            [Option(Description = "Overwrite existing data")]
            internal bool Force { get; }

            internal string Execute()
            {
                var output = string.IsNullOrEmpty(Output)
                   ? fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), $"{Name}.wallet.json")
                   : Output;

                var (chain, chainPath) = fileSystem.LoadExpressChain(Input);
                var wallet = chain.GetWallet(Name);

                if (wallet == null)
                {
                    throw new Exception($"{Name} express wallet not found.");
                }

                if (fileSystem.File.Exists(output))
                {
                    if (Force)
                    {
                        fileSystem.File.Delete(output);
                    }
                    else
                    {
                        throw new Exception("You must specify force to overwrite an exported wallet.");
                    }
                }

                var password = Prompt.GetPassword("Input password to use for exported wallet");
                var devWallet = DevWallet.FromExpressWallet(chain.GetProtocolSettings(), wallet);
                devWallet.Export(output, password);
                return output;
            }

            private int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var output = Execute();
                    console.WriteLine($"{Name} privatenet wallet exported to {output}");
                    return 0;
                }
                catch (Exception ex)
                {
                    app.WriteException(ex);
                    return 1;
                }
            }
        }
    }
}
