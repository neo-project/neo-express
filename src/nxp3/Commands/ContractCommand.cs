using McMaster.Extensions.CommandLineUtils;

namespace nxp3.Commands
{
    [Command("contract")]
    [Subcommand(typeof(Deploy), typeof(Invoke))]
    partial class ContractCommand
    {
        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp();
            return 1;
        }
    }
}
