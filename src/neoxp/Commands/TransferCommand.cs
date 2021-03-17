using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using OneOf;
using All = OneOf.Types.All;
namespace NeoExpress.Commands
{
    [Command("transfer", Description = "Transfer asset between accounts")]
    class TransferCommand
    {
        readonly IExpressChainManagerFactory chainManagerFactory;

        public TransferCommand(IExpressChainManagerFactory chainManagerFactory)
        {
            this.chainManagerFactory = chainManagerFactory;
        }

        [Argument(0, Description = "Amount to transfer")]
        [Required]
        internal string Quantity { get; init; } = string.Empty;

        [Argument(1, Description = "Asset to transfer (symbol or script hash)")]
        [Required]
        internal string Asset { get; init; } = string.Empty;

        [Argument(2, Description = "Account to send asset from")]
        [Required]
        internal string Sender { get; init; } = string.Empty;

        [Argument(3, Description = "Account to send asset to")]
        [Required]
        internal string Receiver { get; init; } = string.Empty;

        [Option(Description = "Path to neo-express data file")]
        internal string Input { get; init; } = string.Empty;

        [Option(Description = "Output as JSON")]
        internal bool Json { get; init; } = false;

        internal static async Task ExecuteAsync(IExpressChainManager chainManager, IExpressNode expressNode, string quantity, string asset, string sender, string receiver, TextWriter writer, bool json = false)
        {
            if (!chainManager.Chain.TryGetAccount(sender, out var senderWallet, out var senderAccount, chainManager.ProtocolSettings))
            {
                throw new Exception($"{sender} sender not found.");
            }

            if (!chainManager.Chain.TryGetAccount(receiver, out _, out var receiverAccount, chainManager.ProtocolSettings))
            {
                throw new Exception($"{receiver} receiver not found.");
            }

            var assetHash = await expressNode.ParseAssetAsync(asset).ConfigureAwait(false);
            var txHash = await expressNode.TransferAsync(assetHash, ParseQuantity(quantity), senderWallet, senderAccount.ScriptHash, receiverAccount.ScriptHash);
            await writer.WriteTxHashAsync(txHash, "Transfer", json).ConfigureAwait(false);

            static OneOf<decimal, All> ParseQuantity(string quantity)
            {
                if ("all".Equals(quantity, StringComparison.OrdinalIgnoreCase))
                {
                    return new All();
                }

                if (decimal.TryParse(quantity, out var amount))
                {
                    return amount;
                }

                throw new Exception($"Invalid quantity value {quantity}");
            }
        }

        internal async Task<int> OnExecuteAsync(IConsole console)
        {
            try
            {
                var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                using var expressNode = chainManager.GetExpressNode();
                await ExecuteAsync(chainManager, expressNode, Quantity, Asset, Sender, Receiver, console.Out, Json).ConfigureAwait(false);
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
