using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit;

namespace NeoExpress.Commands
{
    partial class WalletCommand
    {
        [Command("delete", Description = "Delete neo-express wallet")]
        internal class Delete
        {
            readonly IExpressChain expressFile;

            public Delete(IExpressChain expressFile)
            {
                this.expressFile = expressFile;
            }

            public Delete(CommandLineApplication app) : this(app.GetExpressFile())
            {
            }


            [Argument(0, Description = "Wallet name")]
            [Required]
            internal string Name { get; init; } = string.Empty;

            [Option(Description = "Overwrite existing data")]
            internal bool Force { get; }

            internal int OnExecute(CommandLineApplication app) => app.Execute(this.Execute);

            internal void Execute(IConsole console)
            {
                var wallet = expressFile.Chain.GetWallet(Name);

                if (wallet == null) throw new Exception($"{Name} privatenet wallet not found.");
                if (!Force) throw new Exception("You must specify force to delete a privatenet wallet.");

                expressFile.Chain.Wallets.Remove(wallet);
                expressFile.SaveChain();
                console.WriteLine($"{Name} privatenet wallet deleted.");
            }
        }
    }
}
