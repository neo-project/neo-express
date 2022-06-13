using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Newtonsoft.Json;

namespace NeoExpress.Commands
{
    partial class ShowCommand
    {
        [Command("transaction", "tx", Description = "Show transaction")]
        internal class Transaction
        {
            readonly IExpressChain chain;

            public Transaction(IExpressChain chain)
            {
                this.chain = chain;
            }

            public Transaction(CommandLineApplication app)
            {
                this.chain = app.GetExpressFile();
            }

            [Argument(0, Description = "Transaction hash")]
            [Required]
            internal string TransactionHash { get; init; } = string.Empty;

            internal Task<int> OnExecuteAsync(CommandLineApplication app) => app.ExecuteAsync(this.ExecuteAsync);

            internal async Task ExecuteAsync(IConsole console)
            {
                using var expressNode = chain.GetExpressNode();
                var (tx, log) = await expressNode.GetTransactionAsync(UInt256.Parse(TransactionHash))
                    .ConfigureAwait(false);

                using var writer = new JsonTextWriter(console.Out) { Formatting = Formatting.Indented };
                using var _ = writer.WriteObject();
                writer.WritePropertyName("transaction");
                writer.WriteJson(tx.ToJson(expressNode.ProtocolSettings));
                if (log is not null)
                {
                    writer.WritePropertyName("application-log");
                    writer.WriteJson(log.ToJson());
                }
            }
        }
    }
}
