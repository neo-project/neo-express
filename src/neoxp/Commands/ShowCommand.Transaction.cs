// Copyright (C) 2023 neo-project
//
// The neo-examples-csharp is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace NeoExpress.Commands
{
    partial class ShowCommand
    {
        [Command("transaction", "tx", Description = "Show transaction")]
        internal class Transaction
        {
            readonly ExpressChainManagerFactory chainManagerFactory;

            public Transaction(ExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, Description = "Transaction hash")]
            [Required]
            internal string TransactionHash { get; init; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    using var expressNode = chainManager.GetExpressNode();
                    var (tx, log) = await expressNode.GetTransactionAsync(Neo.UInt256.Parse(TransactionHash));

                    using var writer = new JsonTextWriter(console.Out) { Formatting = Formatting.Indented };
                    await writer.WriteStartObjectAsync();
                    await writer.WritePropertyNameAsync("transaction");
                    writer.WriteJson(tx.ToJson(chainManager.ProtocolSettings));
                    if (log is not null)
                    {
                        await writer.WritePropertyNameAsync("application-log");
                        writer.WriteJson(log.ToJson());
                    }
                    await writer.WriteEndObjectAsync();

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
