using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Models;

namespace NeoExpress.Commands
{
    partial class WalletCommand
    {
        [Command("export", Description = "Export neo-express wallet in NEP-6 format")]
        class Export
        {
            readonly IChainManager chainManager;
            readonly IFileSystem fileSystem;

            public Export(IChainManager chainManager, IFileSystem fileSystem)
            {
                this.fileSystem = fileSystem;
                this.chainManager = chainManager;
            }

            [Argument(0, Description = "Wallet name")]
            [Required]
            private string Name { get; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            private string Input { get; } = string.Empty;

            [Option(Description = "NEP-5 wallet name (Defaults to Neo-Express name if unspecified)")]
            private string Output { get; } = string.Empty;

            [Option(Description = "Overwrite existing data")]
            private bool Force { get; }

            internal string Execute()
            {
                var output = string.IsNullOrEmpty(Output)
                   ? fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), $"{Name}.wallet.json")
                   : Output;

                var (chain, _) = chainManager.Load(Input);
                var wallet = chain.GetWallet(Name);
                if (wallet == null)
                {
                    throw new Exception($"{Name} privatenet wallet not found.");
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
                var devWallet = DevWallet.FromExpressWallet(wallet);
                devWallet.Export(output, password);
                return output;
            }

            private int OnExecute(IConsole console)
            {
                try
                {
                    var output = Execute();
                    console.WriteLine($"{Name} privatenet wallet exported to {output}");
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
