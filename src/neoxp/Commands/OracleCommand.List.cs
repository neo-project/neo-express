using System;
using System.IO;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    partial class OracleCommand
    {
        [Command("list", Description = "List oracle nodes")]
        internal class List
        {
            readonly IExpressChain chain;

            public List(IExpressChain chain)
            {
                this.chain = chain;
            }

            public List(CommandLineApplication app)
            {
                this.chain = app.GetExpressFile();
            }

            // internal async Task ExecuteAsync(TextWriter writer)
            // {
            //     var (chainManager, _) = chainManagerFactory.LoadChain(Input);
            //     var expressNode = chainManager.GetExpressNode();
            //     var oracleNodes = await expressNode.ListOracleNodesAsync();

            //     await writer.WriteLineAsync($"Oracle Nodes ({oracleNodes.Count}): ").ConfigureAwait(false);
            //     for (var x = 0; x < oracleNodes.Count; x++)
            //     {
            //         await writer.WriteLineAsync($"  {oracleNodes[x]}").ConfigureAwait(false);
            //     }
            // }

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    // await ExecuteAsync(console.Out);
                    return 0;
                }
                catch (Exception ex)
                {
                    app.WriteException(ex);
                    return 1;
                }
            }
        }
    }
}
