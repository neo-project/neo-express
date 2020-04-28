using McMaster.Extensions.CommandLineUtils;

namespace nxp3.Commands
{
    [Command("checkpoint")]
    [Subcommand(typeof(Create), typeof(Restore), typeof(Run))]
    partial class CheckpointCommand
    {
        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp();
            return 1;
        }
    }
}
