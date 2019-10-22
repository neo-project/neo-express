using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using NeoExpress.Models;

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
                var senderAccount = chain.GetAccount(Sender);
                if (senderAccount == null)
                {
                    throw new Exception($"{Sender} sender not found.");
                }

                var receiverAccount = chain.GetAccount(Receiver);
                if (receiverAccount == null)
                {
                    throw new Exception($"{Receiver} receiver not found.");
                }

                var uri = chain.GetUri();
                var result = await NeoRpcClient.ExpressTransfer(uri, Asset, Quantity, senderAccount.ScriptHash, receiverAccount.ScriptHash)
                    .ConfigureAwait(false);
                console.WriteResult(result);

                var txid = result?["txid"];
                if (txid != null)
                {
                    console.WriteLine("transfer complete");
                }
                else
                {
                    var signatures = senderAccount.Sign(chain.ConsensusNodes, result);
                    var result2 = await NeoRpcClient.ExpressSubmitSignatures(uri, result?["contract-context"], signatures);
                    console.WriteResult(result2);
                }

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
