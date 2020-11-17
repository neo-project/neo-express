using McMaster.Extensions.CommandLineUtils;

namespace nxp3.Commands
{
    [Command("oracle")]
    [Subcommand(typeof(Enable), typeof(List), typeof(Requests), typeof(Response))]
    partial class OracleCommand
    {
        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp(false);
            return 1;
        }
    }
}
