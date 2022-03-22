using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    [Command("show", Description = "Show information")]
    [Subcommand(typeof(Balance), typeof(Balances), typeof(Block), typeof(Notifications), typeof(Transaction))]
    partial class ShowCommand
    {
        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp(false);
            return 1;
        }
    }
}
