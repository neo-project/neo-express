using McMaster.Extensions.CommandLineUtils;

namespace nxp3.Commands
{
    [Command("contract")]
    [Subcommand(typeof(Deploy), typeof(Get), typeof(Invoke), typeof(List), typeof(Storage))]
    partial class ContractCommand
    {
        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp(false);
            return 1;
        }
    }
}
