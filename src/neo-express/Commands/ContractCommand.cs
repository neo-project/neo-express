using McMaster.Extensions.CommandLineUtils;

namespace Neo.Express.Commands
{
    [Command(Name = "contract")]
    [Subcommand(
        typeof(Deploy),
        typeof(Get),
        typeof(Import),
        typeof(Invoke),
        typeof(List),
        typeof(Storage))]
    internal partial class ContractCommand
    {
        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteError("You must specify at a subcommand.");
            app.ShowHelp();
            return 1;
        }
    }
}
