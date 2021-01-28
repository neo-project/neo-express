using System;
using System.IO;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    partial class OracleCommand
    {
        [Command("list", Description = "List oracle nodes")]
        class List
        {
            readonly IExpressChainManagerFactory chainManagerFactory;

            public List(IExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            internal async Task ExecuteAsync(TextWriter writer)
            {
                var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                var expressNode = chainManager.GetExpressNode();
                var oracleNodes = await expressNode.GetOracleNodesAsync();

                await writer.WriteLineAsync($"Oracle Nodes ({oracleNodes.Length}): ").ConfigureAwait(false);
                for (var x = 0; x < oracleNodes.Length; x++)
                {
                    await writer.WriteLineAsync($"  {oracleNodes[x]}").ConfigureAwait(false);
                }
            }

            internal async Task<int> OnExecuteAsync(IConsole console)
            {
                try
                {
                    await ExecuteAsync(console.Out);
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
