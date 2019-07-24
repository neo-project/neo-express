using McMaster.Extensions.CommandLineUtils;

namespace Neo.Express.Commands
{
    [Command("wallet")]
    [Subcommand(typeof(Create), typeof(Delete), typeof(Export), typeof(List))]
    internal partial class WalletCommand
    {
        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp();
            return 1;
        }
    }
}
