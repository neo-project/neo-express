using System;
using System.IO;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Neo3;

namespace nxp3.Commands
{
    [Command("export")]
    class ExportCommand
    {
        [Option]
        string Input { get; } = string.Empty;

        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            try
            {
                var (chain, _) = Program.LoadExpressChain(Input);
                var password = Prompt.GetPassword("Input password to use for exported wallets");

                var blockchainOperations = new BlockchainOperations();
                blockchainOperations.ExportBlockchain(chain, Directory.GetCurrentDirectory(), password, console.Out);

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
