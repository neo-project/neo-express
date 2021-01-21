using System;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Neo3;

namespace nxp3.Commands
{
    [Command("transfer", Description = "Transfer asset between accounts")]
    class TransferCommand
    {
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

        internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
        {
            try
            {
                var (chain, _) = Program.LoadExpressChain(Input);
                var blockchainOperations = new BlockchainOperations();
                var senderAccount = blockchainOperations.GetAccount(chain, Sender);
                if (senderAccount == null)
                {
                    throw new Exception($"{Sender} sender not found.");
                }

                var receiverAccount = blockchainOperations.GetAccount(chain, Receiver);
                if (receiverAccount == null)
                {
                    throw new Exception($"{Receiver} receiver not found.");
                }

                var txHash = await blockchainOperations.Transfer(chain, Asset, Quantity, senderAccount, receiverAccount)
                    .ConfigureAwait(false);
                if (Json)
                {
                    console.WriteLine($"{txHash}");
                }
                else
                {
                    console.WriteLine($"Transfer Transaction {txHash} submitted");
                }

                return 0;
            }
            catch (Exception ex)
            {
                console.Error.WriteLine(ex.Message);
                return 1;
            }
        }
    }
}
