using System;
using System.IO.Abstractions;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    partial class ShowCommand
    {
        [Command("block", Description = "Show block")]
        internal class Block
        {
            readonly IFileSystem fileSystem;

            public Block(IFileSystem fileSystem)
            {
                this.fileSystem = fileSystem;
            }

            [Argument(0, Description = "Optional block hash or index. Show most recent block if unspecified")]
            internal string BlockHash { get; init; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chainManager, _) = fileSystem.LoadChainManager(Input);
                    using var expressNode = chainManager.Chain.GetExpressNode(fileSystem);
                    var block = await expressNode.GetBlockAsync(BlockHash).ConfigureAwait(false);
                    console.WriteJson(block.ToJson(chainManager.ProtocolSettings));
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
