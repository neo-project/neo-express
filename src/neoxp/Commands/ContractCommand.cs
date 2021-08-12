using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    [Command("contract", Description = "Manage smart contracts")]
    [Subcommand(typeof(Deploy), typeof(Get), typeof(Hash), typeof(Invoke), typeof(List), typeof(Run), typeof(Storage))]
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
