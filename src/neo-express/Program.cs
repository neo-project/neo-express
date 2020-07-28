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

        public static int Main(string[] args)
            => CommandLineApplication.Execute<Program>(args);

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
            var chain = Abstractions.Models.ExpressChain.Load(filename);
            return (chain, filename);
        }
    }
}
