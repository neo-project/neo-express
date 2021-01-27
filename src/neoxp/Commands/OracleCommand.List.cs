using System;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    partial class OracleCommand
    {
        [Command("list", Description = "List oracle nodes")]
        class List
        {
            readonly IBlockchainOperations blockchainOperations;

            public List(IBlockchainOperations blockchainOperations)
            {
                this.blockchainOperations = blockchainOperations;
            }

            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = blockchainOperations.LoadChain(Input);
                    // var (chain, _) = Program.LoadExpressChain(Input);
                    // var blockchainOperations = new BlockchainOperations();
                    // var oracleNodes = await blockchainOperations
                    //     .GetOracleNodesAsync(chain)
                    //     .ConfigureAwait(false);
                    
                    // console.WriteLine($"Oracle Nodes ({oracleNodes.Length}): ");
                    // for (var x = 0; x < oracleNodes.Length; x++)
                    // {
                    //     console.WriteLine($"  {oracleNodes[x]}");
                    // }

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
