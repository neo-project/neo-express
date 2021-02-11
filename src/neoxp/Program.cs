using System;
using System.IO.Abstractions;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using NeoExpress.Commands;

namespace NeoExpress
{
    [Command("nxp3", Description = "Neo 3 blockchain private net for developers", UsePagerForHelpText = false)]
    [Subcommand(
        typeof(BatchCommand),
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
        public static Task<int> Main(string[] args)
        {
            EnableAnsiEscapeSequences();

            var services = new ServiceCollection()
                .AddSingleton<IExpressChainManagerFactory, ExpressChainManagerFactory>()
                .AddSingleton<IFileSystem, FileSystem>()
                .BuildServiceProvider();

            var app = new CommandLineApplication<Program>();
            app.Conventions
                .UseDefaultConventions()
                .UseConstructorInjection(services);

            return app.ExecuteAsync(args);
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

        static void EnableAnsiEscapeSequences()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                const int STD_OUTPUT_HANDLE = -11;
                var stdOutHandle = GetStdHandle(STD_OUTPUT_HANDLE);

                if (GetConsoleMode(stdOutHandle, out uint outMode))
                {
                    const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
                    const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

                    outMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;
                    SetConsoleMode(stdOutHandle, outMode);
                }
            }
        }

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);
    }
}
