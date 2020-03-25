using McMaster.Extensions.CommandLineUtils;
using System;
using System.Threading.Tasks;
using NeoExpress.Abstractions;
using NeoExpress.Neo2;

namespace NeoExpress.Commands
{
    [Command("transfer")]
    internal class TransferCommand
    {
        [Argument(0)]
        private string Asset { get; } = string.Empty;

        [Argument(1)]
        private string Quantity { get; } = string.Empty;

        [Argument(2)]
        private string Sender { get; } = string.Empty;

        [Argument(3)]
        private string Receiver { get; } = string.Empty;

        [Option]
        private string Input { get; } = string.Empty;

        private async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
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

                var tx = await blockchainOperations.Transfer(chain,
                                                             Asset,
                                                             Quantity,
                                                             senderAccount,
                                                             receiverAccount);

                console.WriteLine($"Transfer Transaction {tx.Hash} submitted");
                return 0;
            }
            catch (Exception ex)
            {
                console.WriteError(ex.Message);
                app.ShowHelp();
                return 1;
            }
        }
    }
}
