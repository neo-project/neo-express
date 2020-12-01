using System;
using System.IO;
using System.Runtime.InteropServices;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Abstractions.Models;
using nxp3.Commands;

namespace nxp3
{
    [Command("nxp3", UsePagerForHelpText = false)]
    [Subcommand(
        typeof(CheckpointCommand),
        typeof(ContractCommand),
        typeof(CreateCommand),
        typeof(ExportCommand),
        typeof(OracleCommand),
        typeof(ResetCommand),
        typeof(RunCommand),
        typeof(ShowCommand),
        typeof(TransferCommand),
        typeof(WalletCommand))]
    class Program
    {
        public static int Main(string[] args)
        {
            var iStdOut = GetStdHandle(STD_OUTPUT_HANDLE);
            if (!GetConsoleMode(iStdOut, out uint outConsoleMode))
            {
                Console.WriteLine("failed to get output console mode");
                return 1;
            }

            outConsoleMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;
            if (!SetConsoleMode(iStdOut, outConsoleMode))
            {
                Console.WriteLine($"failed to set output console mode, error code: {GetLastError()}");
                return 1;
            }

            return CommandLineApplication.Execute<Program>(args);
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
            app.ShowHelp(false);
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

        private const int STD_OUTPUT_HANDLE = -11;
        private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        private const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        public static extern uint GetLastError();
    }
}
