using System.ComponentModel.DataAnnotations;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using static Neo.BlockchainToolkit.Utility;

namespace NeoTrace.Commands
{
    [Command("transaction", "tx", Description = "Trace the specified transaction")]
    class TransactionCommand
    {
        [Argument(0, Description = "Block index or hash")]
        [Required]
        internal string TransactionHash { get; } = string.Empty;

        [Option(Description = "URL of Neo JSON-RPC Node\nSpecify MainNet (default), TestNet or JSON-RPC URL")]
        internal string RpcUri { get; } = string.Empty;

        internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
        {
            try
            {
                if (!TryParseRpcUri(RpcUri, out var uri))
                {
                    throw new ArgumentException($"Invalid RpcUri value \"{RpcUri}\"");
                }
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