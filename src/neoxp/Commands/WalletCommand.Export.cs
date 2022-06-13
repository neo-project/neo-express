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

            internal int OnExecute(CommandLineApplication app) => app.Execute(this.Execute);

            internal void Execute(IFileSystem fileSystem, IConsole console)
            {
                var output = string.IsNullOrEmpty(Output)
                   ? fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), $"{Name}.wallet.json")
                   : Output;

                if (fileSystem.File.Exists(output) && !Force)
                {
                    throw new Exception("You must specify force to overwrite an exported wallet.");
                }

                var wallet = chain.GetWallet(Name) ?? throw new Exception($"{Name} express wallet not found.");
                var password = Prompt.GetPassword("Input password to use for exported wallet");
                var json = wallet.ExportNEP6(password, chain.AddressVersion);
                fileSystem.File.WriteAllText(output, json.ToString());
                console.WriteLine($"{Name} privatenet wallet exported to {output}");
            }
        }
    }
}
