using McMaster.Extensions.CommandLineUtils;
using Neo.Express.Commands;
using System.IO;

namespace Neo.Express
{
    [Command("neo-express")]
    [Subcommand(
        typeof(CheckPointCommand),
        typeof(ClaimCommand),
        typeof(ContractCommand),
        typeof(CreateCommand),
        typeof(RunCommand),
        typeof(ExportCommand),
        typeof(ShowCommand),
        typeof(TransferCommand),
        typeof(WalletCommand))]
    internal class Program
    {
        private static int Main(string[] args) => CommandLineApplication.Execute<Program>(args);

        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify a subcommand.");
            app.ShowHelp();
            return 1;
        }
    }
}
