using McMaster.Extensions.CommandLineUtils;
using Neo.Express.Commands;
using System;
using System.IO;
using System.Reflection;

namespace Neo.Express
{
    [Command("neo-express")]
    [Subcommand(
        //typeof(CheckPointCommand),
        //typeof(ClaimCommand),
        //typeof(ContractCommand),
        typeof(CreateCommand)
        //typeof(RunCommand),
        //typeof(ExportCommand),
        //typeof(ShowCommand),
        //typeof(TransferCommand),
        //typeof(WalletCommand)
        )]
    internal class Program
    {
        public static string ROOT_PATH => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NEO-Express", "blockchain-nodes");

        private static int Main(string[] args)
        {
            using (var app = new CommandLineApplication<Program>())
            {
                app.Conventions.UseDefaultConventions();
                app.UsePagerForHelpText = false;
                return app.Execute(args);
            }
        }

        [Option]
        private bool Version { get; }

        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            if (Version)
            {
                var versionAttribute = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                console.WriteLine(versionAttribute == null ? "unknown version" : versionAttribute.InformationalVersion);
                return 0;
            }

            console.WriteLine("You must specify a subcommand.");
            app.ShowHelp();
            return 1;
        }

        public static string GetDefaultFilename(string filename) => string.IsNullOrEmpty(filename)
           ? Path.Combine(Directory.GetCurrentDirectory(), "default.neo-express.json")
           : filename;

        public static Abstractions.INeoBackend GetBackend()
        {
            return new Backend2.Neo2Backend();
        }
    }
}
