using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;

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

                var blockchainOperations = new NeoExpress.Neo2.BlockchainOperations();
                blockchainOperations.ExportBlockchain(chain, Directory.GetCurrentDirectory(), password, console.Out);

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
