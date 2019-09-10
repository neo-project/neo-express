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
        private string Asset { get; }

        [Argument(1)]
        private string Quantity { get; }

        [Argument(2)]
        private string Sender { get; }

        [Argument(3)]
        private string Receiver { get; }

        [Option]
        private string Input { get; }

        private static JArray GetGenesisSignatures(ExpressChain chain, IEnumerable<string> hashes, byte[] data)
        {
            return null;
            //var backend = Program.GetBackend();
            //var signatures = chain.ConsensusNodes.SelectMany(n => n.Wallet.Sign(hashes, data, backend));
            //return new JArray(signatures);
        }

        private static JArray GetStandardSignatures(ExpressWalletAccount account, byte[] data)
        {
            return new JArray(account.Sign(data));
        }

        private async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
        {
            try
            {
                var (chain, _) = Program.LoadExpressChain(Input);
                var senderAccount = chain.GetAccount(Sender);
                if (senderAccount == default)
                {
                    throw new Exception($"{Sender} sender not found.");
                }

                var receiverAccount = chain.GetAccount(Receiver);
                if (receiverAccount == default)
                {
                    throw new Exception($"{Receiver} receiver not found.");
                }

                var uri = chain.GetUri();
                var result = await NeoRpcClient.ExpressTransfer(uri, Asset, Quantity, senderAccount.ScriptHash, receiverAccount.ScriptHash)
                    .ConfigureAwait(false);
                console.WriteLine(result.ToString(Formatting.Indented));

                var txid = result["txid"];
                if (txid != null)
                {
                    console.WriteLine("transfer complete");
                }
                else
                {
                    var data = result.Value<string>("hash-data").ToByteArray();
                    var signatures = senderAccount.Sign(chain.ConsensusNodes, result);
                    var result2 = await NeoRpcClient.ExpressSubmitSignatures(uri, result["contract-context"], signatures);
                    console.WriteLine(result2.ToString(Formatting.Indented));
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
