using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;

namespace NeoTrace.Commands
{
    [Command("transaction", "tx", Description = "")]
    class TransactionCommand
    {
        [Argument(0, Description = "Block index or hash")]
        [Required]
        internal string TransactionHash { get; } = string.Empty;

        [Option]
        internal string RpcUri { get; } = string.Empty;

        internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
        {
            try
            {
                var uri = Program.ParseRpcUri(RpcUri);
                var txHash = UInt256.TryParse(TransactionHash, out var _txHash)
                    ? _txHash 
                    : throw new ArgumentException($"Invalid transaction hash {TransactionHash}");
                await Program.TraceTransactionAsync(uri, txHash, console);
                return 0;
            }
            catch (Exception ex)
            {
                await app.Error.WriteLineAsync(ex.Message);
                return 1;
            }
        }
    }
}