using System;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using Newtonsoft.Json;

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

            public Block(CommandLineApplication app) : this(app.GetExpressFile())
            {
            }

            [Argument(0, Description = "Optional block hash or index. Show most recent block if unspecified")]
            internal string BlockHash { get; init; } = string.Empty;

            internal Task<int> OnExecuteAsync(CommandLineApplication app) => app.ExecuteAsync(this.ExecuteAsync);

            internal async Task ExecuteAsync(IConsole console)
            {
                using var expressNode = chain.GetExpressNode();
                var blockTask = string.IsNullOrEmpty(BlockHash)
                    ? expressNode.GetLatestBlockAsync()
                    : UInt256.TryParse(BlockHash, out var hash)
                        ? expressNode.GetBlockAsync(hash)
                        : uint.TryParse(BlockHash, out var index)
                            ? expressNode.GetBlockAsync(index)
                            : throw new ArgumentException();
                var block = await blockTask.ConfigureAwait(false);
                console.WriteJson(block.ToJson(expressNode.ProtocolSettings));
            }
        }
    }
}

  