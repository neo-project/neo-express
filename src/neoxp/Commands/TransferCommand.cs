using System;
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

        [Argument(0, Description = "Asset to transfer (symbol or script hash)")]
        string Asset { get; } = string.Empty;

        [Argument(1, Description = "Amount to transfer")]
        string Quantity { get; } = string.Empty;

        [Argument(2, Description = "Account to send asset from")]
        string Sender { get; } = string.Empty;

        [Argument(3, Description = "Account to send asset to")]
        string Receiver { get; } = string.Empty;

        [Option(Description = "Path to neo-express data file")]
        string Input { get; } = string.Empty;

        [Option(Description = "Output as JSON")]
        bool Json { get; } = false;

        OneOf<decimal, All> ParseQuantity()
        {
            if ("all".Equals(Quantity, StringComparison.OrdinalIgnoreCase))
            {
                return new All();
            }

            if (decimal.TryParse(Quantity, out var amount))
            {
                return amount;
            }

            throw new Exception($"Invalid quantity value {Quantity}");
        }

        private async Task<UInt256> ExecuteAsync()
        {
            var quantity = ParseQuantity();
            var (chainManager, _) = chainManagerFactory.LoadChain(Input);
            var chain = chainManager.Chain;
            var sender = chain.GetAccount(Sender) ?? throw new Exception($"{Sender} sender not found.");
            var receiver = chain.GetAccount(Receiver) ?? throw new Exception($"{Receiver} receiver not found.");

            using var expressNode = chainManager.GetExpressNode();
            var assetHash = await expressNode.ParseAssetAsync(Asset).ConfigureAwait(false);
            return await expressNode.TransferAsync(assetHash, quantity, sender, receiver);
        }

        internal async Task<int> OnExecuteAsync(IConsole console)
        {
            try
            {
                var txHash = await ExecuteAsync().ConfigureAwait(false);
                if (Json)
                {
                    await console.Out.WriteLineAsync($"{txHash}");
                }
                else
                {
                    await console.Out.WriteLineAsync($"Transfer Transaction {txHash} submitted");
                }
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
