using System;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace nxp3.Commands
{
    partial class OracleCommand
    {
        [Command("list")]
        class List
        {
            [Option]
            string Input { get; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);
                    var blockchainOperations = new NeoExpress.Neo3.BlockchainOperations();
                    var oracleNodes = await blockchainOperations
                        .GetOracleNodes(chain)
                        .ConfigureAwait(false);
                    
                    console.WriteLine($"Oracle Nodes ({oracleNodes.Length}): ");
                    for (var x = 0; x < oracleNodes.Length; x++)
                    {
                        console.WriteLine($"  {oracleNodes[x]}");
                    }

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
}
