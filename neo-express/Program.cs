using McMaster.Extensions.CommandLineUtils;
using Neo.Express.Commands;

namespace Neo.Express
{
    [Command("neo-express")]
    [Subcommand(typeof(CreateCommand))]
    class Program
    {
        static int Main(string[] args) => CommandLineApplication.Execute<Program>(args);

        int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify a subcommand.");
            app.ShowHelp();
            return 1;
        }
    }
}
