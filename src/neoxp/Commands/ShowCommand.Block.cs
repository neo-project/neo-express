using System;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;


namespace NeoExpress.Commands
{
    partial class ShowCommand
    {
        [Command("block", Description = "Show block")]
        class Block
        {
            readonly IExpressChainManagerFactory chainManagerFactory;

            public Block(IExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, Description = "Optional block hash or index. Show most recent block if unspecified")]
            string BlockHash { get; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    // var (chain, _) = Program.LoadExpressChain(Input);
                    // var blockchainOperations = new BlockchainOperations();

                    // var block = await blockchainOperations.ShowBlockAsync(chain, BlockHash).ConfigureAwait(false);
                    // console.WriteLine(block.ToJson().ToString(true));
                    return 0;
                }
                catch (Exception ex)
                {
                    await console.Error.WriteLineAsync(ex.Message);
                    return 1;
                }
            }

        }
    }
}
