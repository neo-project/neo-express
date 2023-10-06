// Copyright (C) 2023 neo-project
//
// The neo-examples-csharp is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using McMaster.Extensions.CommandLineUtils;
using System;
using System.Threading.Tasks;

namespace NeoExpress.Commands
{
    partial class ShowCommand
    {
        [Command("block", Description = "Show block")]
        internal class Block
        {
            readonly ExpressChainManagerFactory chainManagerFactory;

            public Block(ExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, Description = "Optional block hash or index. Show most recent block if unspecified")]
            internal string BlockHash { get; init; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    using var expressNode = chainManager.GetExpressNode();
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
