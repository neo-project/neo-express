using System;
using System.IO.Abstractions;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using NeoExpress.Commands;

namespace NeoExpress
{
    [Command("neoxp", Description = "Neo N3 blockchain private net for developers", UsePagerForHelpText = false)]
    [VersionOption(ThisAssembly.AssemblyInformationalVersion)]
    [Subcommand(
        typeof(BatchCommand),
        typeof(CheckpointCommand),
        typeof(ContractCommand),
        typeof(CreateCommand),
        typeof(ExportCommand),
        typeof(FastForwardCommand),
        typeof(OracleCommand),
        typeof(PolicyCommand),
        typeof(ResyncCommand),
        typeof(ResetCommand),
        typeof(RunCommand),
        typeof(ShowCommand),
        typeof(StopCommand),
        typeof(TransferCommand),
        typeof(WalletCommand))]
    class Program
    {
        public static async Task<int> Main(string[] args)
        {
            EnableAnsiEscapeSequences();

            var services = new ServiceCollection()
                .AddSingleton<ExpressChainManagerFactory>()
                .AddSingleton<TransactionExecutorFactory>()
                .AddSingleton<IFileSystem, FileSystem>()
                .AddSingleton<IConsole>(PhysicalConsole.Singleton)
                .BuildServiceProvider();

            var app = new CommandLineApplication<Program>();
            app.Option<bool>("--stack-trace", "", CommandOptionType.NoValue, o => o.ShowInHelpText = false, inherited: true);
            app.Conventions
                .UseDefaultConventions()
                .UseConstructorInjection(services);

            try
            {
                return await app.ExecuteAsync(args);
            }
            catch (CommandParsingException ex)
            {
                await Console.Error.WriteLineAsync($"\x1b[1m\x1b[31m\x1b[40m{ex.Message}");

                if (ex is UnrecognizedCommandParsingException uex && uex.NearestMatches.Any())
                {
                    await Console.Error.WriteLineAsync();
                    await Console.Error.WriteLineAsync("Did you mean this?");
                    await Console.Error.WriteLineAsync("    " + uex.NearestMatches.First());
                }

                return 1;
            }
            catch (Exception ex)
            {
                app.WriteException(ex);
                return -1;
            }
        }

        private int OnExecute(CommandLineApplication app, IConsole console)
        {
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
