using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    [Command("wallet", Description = "Manage neo-express wallets")]
    [Subcommand(typeof(Create), typeof(Delete), typeof(Export), typeof(List))]
    partial class WalletCommand
    {
        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp(false);
            return 1;
        }
    }
}
