using McMaster.Extensions.CommandLineUtils;
using Neo.Express.Commands;
using System.IO;

namespace Neo.Express
{
    [Command("neo-express")]
    [Subcommand(typeof(CreateCommand), typeof(RunCommand), typeof(ExportCommand))]
    class Program
    {
        static int Main(string[] args) => CommandLineApplication.Execute<Program>(args);

        int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify a subcommand.");
            app.ShowHelp();
            return 1;
        }

        public static string DefaultPrivatenetFileName(string filename)
        {
            return string.IsNullOrEmpty(filename)
               ? Path.Combine(Directory.GetCurrentDirectory(), "express.privatenet.json")
               : filename;
        }
    }
}
