using McMaster.Extensions.CommandLineUtils;
using System.Text;

namespace Neo.Express.Commands
{
    [Command(Name = "contract")]
    [Subcommand(typeof(Deploy), typeof(Get), typeof(Import), typeof(Invoke), typeof(List))]
    internal partial class ContractCommand
    {
        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp();
            return 1;
        }
    }
}
