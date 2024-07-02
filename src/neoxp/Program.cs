// Copyright (C) 2015-2024 The Neo Project.
//
// Program.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using NeoExpress.Commands;
using System.IO.Abstractions;
using System.Runtime.InteropServices;

namespace NeoExpress
{
    [Command("neoxp", Description = "Neo N3 blockchain private net for developers", UsePagerForHelpText = false)]
    [VersionOption(ThisAssembly.AssemblyInformationalVersion)]
    [Subcommand(
        typeof(BatchCommand),
        typeof(CheckpointCommand),
        typeof(CandidateCommand),
        typeof(ContractCommand),
        typeof(CreateCommand),
        typeof(ExecuteCommand),
        typeof(ExportCommand),
        typeof(FastForwardCommand),
        typeof(OracleCommand),
        typeof(PolicyCommand),
        typeof(ResetCommand),
        typeof(RunCommand),
        typeof(ShowCommand),
        typeof(StopCommand),
        typeof(TransferCommand),
        typeof(TransferNFTCommand),
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
                await Console.Error.WriteLineAsync($"\x1b[1m\x1b[31m\x1b[40m{ex.Message}\x1b[0m");
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
