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
        internal class Export
        {
            readonly IExpressChain chain;

            public Export(IExpressChain chain)
            {
                this.chain = chain;
            }

            public Export(CommandLineApplication app)
            {
                this.chain = app.GetExpressFile();
            }

            [Argument(0, Description = "Wallet name")]
            [Required]
            internal string Name { get; init; } = string.Empty;

            [Option(Description = "NEP-6 wallet name (Defaults to Neo-Express name if unspecified)")]
            internal string Output { get; init; } = string.Empty;

            [Option(Description = "Overwrite existing data")]
            internal bool Force { get; }

            // internal string Execute()
            // {
            //     var output = string.IsNullOrEmpty(Output)
            //        ? fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), $"{Name}.wallet.json")
            //        : Output;

            //     var (chainManager, chainPath) = chainManagerFactory.LoadChain(Input);
            //     var wallet = chainManager.Chain.GetWallet(Name);

            //     if (wallet is null)
            //     {
            //         throw new Exception($"{Name} express wallet not found.");
            //     }

            //     if (fileSystem.File.Exists(output))
            //     {
            //         if (Force)
            //         {
            //             fileSystem.File.Delete(output);
            //         }
            //         else
            //         {
            //             throw new Exception("You must specify force to overwrite an exported wallet.");
            //         }
            //     }

            //     var password = Prompt.GetPassword("Input password to use for exported wallet");
            //     var devWallet = DevWallet.FromExpressWallet(chainManager.ProtocolSettings, wallet);
            //     devWallet.Export(output, password);
            //     return output;
            // }

            private int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    // var output = Execute();
                    // console.WriteLine($"{Name} privatenet wallet exported to {output}");
                    console.WriteLine($"{nameof(WalletCommand)} {nameof(Export)}");
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
