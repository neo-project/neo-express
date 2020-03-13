using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using NeoExpress.Neo2.Models;
using NeoExpress.Neo2.Node;

namespace NeoExpress.Neo2.Commands
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

                var unspents = (await NeoRpcClient.GetUnspents(uri, senderAccount.ScriptHash)
                    .ConfigureAwait(false))?.ToObject<UnspentsResponse>();
                if (unspents == null)
                {
                    throw new Exception($"could not retrieve unspents for {Sender}");
                }

                var assetId = NodeUtility.GetAssetId(Asset);
                var tx = RpcTransactionManager.CreateContractTransaction(
                        assetId, Quantity, unspents, senderAccount, receiverAccount);

                tx.Witnesses = new[] { RpcTransactionManager.GetWitness(tx, chain, senderAccount) };
                var sendResult = await NeoRpcClient.SendRawTransaction(uri, tx);
                if (sendResult == null || !sendResult.Value<bool>())
                {
                    throw new Exception("SendRawTransaction failed");
                }

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
