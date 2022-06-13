using System;
using System.ComponentModel.DataAnnotations;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    partial class WalletCommand
    {
        [Command("delete", Description = "Delete neo-express wallet")]
        internal class Delete
        {
            readonly IExpressChain chain;

            public Delete(IExpressChain chain)
            {
                this.chain = chain;
            }

            public Delete(CommandLineApplication app)
            {
                this.chain = app.GetExpressFile();
            }

            [Argument(0, Description = "Wallet name")]
            [Required]
            internal string Name { get; init; } = string.Empty;

            [Option(Description = "Overwrite existing data")]
            internal bool Force { get; }

            internal int OnExecute(CommandLineApplication app) => app.Execute(this.Execute);

            internal void Execute(IConsole console)
            {
                var wallet = chain.GetWallet(Name);

                if (wallet is null) throw new Exception($"{Name} privatenet wallet not found.");
                if (!Force) throw new Exception("You must specify force to delete a privatenet wallet.");

                chain.RemoveWallet(wallet);
                chain.SaveChain();
                console.WriteLine($"{Name} privatenet wallet deleted.");
            }
        }
    }
}
