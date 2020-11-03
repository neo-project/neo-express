using McMaster.Extensions.CommandLineUtils;

namespace nxp3.Commands
{
    [Command("wallet")]
    [Subcommand(typeof(Create), typeof(List))]
    partial class WalletCommand
    {
        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp();
            return 1;
        }
    }
}
