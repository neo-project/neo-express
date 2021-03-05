using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    partial class ShowCommand
    {
        [Command("transaction", "tx", Description = "Show transaction")]
        internal class Transaction
        {
            readonly IExpressChainManagerFactory chainManagerFactory;

            public Transaction(IExpressChainManagerFactory chainManagerFactory)
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
                    // console.WriteLine(tx.ToJson().ToString(true));
                    if (log != null) console.WriteLine(log.ToJson().ToString(true));
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
