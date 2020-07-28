using System;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Neo3;

namespace nxp3.Commands
{
    partial class ShowCommand
    {
        [Command("block")]
        class Block
        {
            [Argument(0)]
            private string BlockHash { get; } = string.Empty;

            [Option]
            private string Input { get; } = string.Empty;

            private async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);
                    var blockchainOperations = new BlockchainOperations();

                    var tx = await blockchainOperations.ShowBlock(chain, BlockHash);
                    console.WriteLine(tx.ToJson().ToString(true));
                    return 0;                
                }
                catch (Exception ex)
                {
                    console.WriteLine(ex.Message);
                    app.ShowHelp();
                    return 1;
                }
            }

        }
    }
}
