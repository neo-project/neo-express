using McMaster.Extensions.CommandLineUtils;

namespace nxp3.Commands
{
    [Command("show")]
    [Subcommand(typeof(Balance))]
    partial class ShowCommand
    {
        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp();
            return 1;
        }
    }
}
