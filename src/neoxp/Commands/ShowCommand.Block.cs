using System;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;

namespace NeoExpress.Commands
{
    partial class ShowCommand
    {
        [Command("block", Description = "Show block")]
        internal class Block
        {
            readonly IExpressChain chain;

            public Block(IExpressChain chain)
            {
                this.chain = chain;
            }

            public Block(CommandLineApplication app)
            {
                this.chain = app.GetExpressFile();
            }

            [Argument(0, Description = "Optional block hash or index. Show most recent block if unspecified")]
            internal string BlockIdentifier { get; init; } = string.Empty;

            internal Task<int> OnExecuteAsync(CommandLineApplication app) => app.ExecuteAsync(this.ExecuteAsync);

            internal async Task ExecuteAsync(IConsole console)
            {
                using var expressNode = chain.GetExpressNode();
                var blockTask = string.IsNullOrEmpty(BlockIdentifier)
                    ? expressNode.GetLatestBlockAsync()
                    : UInt256.TryParse(BlockIdentifier, out var hash)
                        ? expressNode.GetBlockAsync(hash)
                        : uint.TryParse(BlockIdentifier, out var index)
                            ? expressNode.GetBlockAsync(index)
                            : throw new ArgumentException(nameof(BlockIdentifier));
                var block = await blockTask.ConfigureAwait(false);
                console.WriteJson(block.ToJson(expressNode.ProtocolSettings));
            }
        }
    }
}
