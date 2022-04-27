using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;

namespace NeoExpress.Commands
{
    partial class ShowCommand
    {
        [Command("transaction", "tx", Description = "Show transaction")]
        internal class Transaction
        {
            readonly IFileSystem fileSystem;

            public Transaction(IFileSystem fileSystem)
            {
                this.fileSystem = fileSystem;
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
                    var (chainManager, _) = fileSystem.LoadChainManager(Input);
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
