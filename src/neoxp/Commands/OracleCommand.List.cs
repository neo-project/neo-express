using System;
using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    partial class OracleCommand
    {
        [Command("list", Description = "List oracle nodes")]
        internal class List
        {
            readonly IFileSystem fileSystem;

            public List(IFileSystem fileSystem)
            {
                this.fileSystem = fileSystem;
            }

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            internal async Task ExecuteAsync(TextWriter writer)
            {
                var (chain, _) = fileSystem.LoadExpressChain(Input);
                var expressNode = chain.GetExpressNode(fileSystem);
                var oracleNodes = await expressNode.ListOracleNodesAsync();

                await writer.WriteLineAsync($"Oracle Nodes ({oracleNodes.Count}): ").ConfigureAwait(false);
                for (var x = 0; x < oracleNodes.Count; x++)
                {
                    await writer.WriteLineAsync($"  {oracleNodes[x]}").ConfigureAwait(false);
                }
            }

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    await ExecuteAsync(console.Out);
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
