using McMaster.Extensions.CommandLineUtils;
using System;
using System.IO;

namespace NeoExpress.Commands
{
    [Command("export")]
    internal class ExportCommand
    {
        [Option]
        private string Input { get; } = string.Empty;

        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            try
            {
                var (chain, _) = Program.LoadExpressChain(Input);
                var password = Prompt.GetPassword("Input password to use for exported wallets");

                BlockchainOperations.ExportBlockchain(chain, Directory.GetCurrentDirectory(), password, msg => console.WriteLine(msg));

                return 0;
            }
            catch (Exception ex)
            {
                console.WriteError(ex.Message);
                app.ShowHelp();
                return 1;
            }
        }
    }
}
