using System;
using System.IO;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Abstractions.Models;
using nxp3.Commands;

namespace nxp3
{
    [Command("nxp3")]
    [Subcommand(
        typeof(ContractCommand),
        typeof(CreateCommand), 
        typeof(RunCommand), 
        typeof(ShowCommand), 
        typeof(TransferCommand), 
        typeof(WalletCommand))]
    class Program
    {
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
           ? Path.Combine(Directory.GetCurrentDirectory(), "default.neo-express")
           : filename;

        public static (ExpressChain chain, string filename) LoadExpressChain(string filename)
        {
            filename = GetDefaultFilename(filename);
            if (!File.Exists(filename))
            {
                throw new Exception($"{filename} file doesn't exist");
            }
            var chain = ExpressChain.Load(filename);
            return (chain, filename);
        }
    }
}
