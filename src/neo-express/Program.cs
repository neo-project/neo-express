using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Abstractions.Models;
using NeoExpress.Commands;
using Newtonsoft.Json;
using System;
using System.IO;

namespace NeoExpress
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
        public static string ROOT_PATH => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Neo-Express", "blockchain-nodes");

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
                console.WriteLine(ThisAssembly.AssemblyInformationalVersion);
                return 0;
            }

            console.WriteLine("You must specify a subcommand.");
            app.ShowHelp();
            return 1;
        }

        public static string GetDefaultFilename(string filename) => string.IsNullOrEmpty(filename)
           ? Path.Combine(Directory.GetCurrentDirectory(), "default.neo-express.json")
           : filename;

        public static (ExpressChain chain, string filename) LoadExpressChain(string filename)
        {
            filename = GetDefaultFilename(filename);
            if (!File.Exists(filename))
            {
                throw new Exception($"{filename} file doesn't exist");
            }

            var serializer = new JsonSerializer();
            using (var stream = File.OpenRead(filename))
            using (var reader = new JsonTextReader(new StreamReader(stream)))
            {
                return (serializer.Deserialize<ExpressChain>(reader), filename);
            }
        }
    }
}
