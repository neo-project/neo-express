using System;
using System.IO;
using McMaster.Extensions.CommandLineUtils;


namespace NeoExpress.Commands
{
    [Command("export", Description = "Export neo-express protocol, config and wallet files")]
    class ExportCommand
    {
        [Option(Description = "Path to neo-express data file")]
        string Input { get; } = string.Empty;

        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            try
            {
                // var (chain, _) = Program.LoadExpressChain(Input);
                // var password = Prompt.GetPassword("Input password to use for exported wallets");

                // var blockchainOperations = new BlockchainOperations();
                // blockchainOperations.ExportBlockchain(chain, Directory.GetCurrentDirectory(), password, console.Out);

                return 0;
            }
            catch (Exception ex)
            {
                console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

    }
}
