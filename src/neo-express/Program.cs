using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

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
                // TODO: remove explicitly setting UsePagerForHelpText once version of CommandLineUtils with
                //       https://github.com/natemcmaster/CommandLineUtils/pull/347 ships
                app.UsePagerForHelpText = false;
                app.Conventions.UseDefaultConventions();
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

        public static (Abstractions.Models.ExpressChain chain, string filename) LoadExpressChain(string filename)
        {
            filename = GetDefaultFilename(filename);
            if (!File.Exists(filename))
            {
                throw new Exception($"{filename} file doesn't exist");
            }

            var serializer = new JsonSerializer();
            using var stream = File.OpenRead(filename);
            using var reader = new JsonTextReader(new StreamReader(stream));
            var chain = serializer.Deserialize<Abstractions.Models.ExpressChain>(reader)
                ?? throw new Exception($"Cannot load Neo-Express instance information from {filename}");

            return (chain, filename);
        }
    }
}
