using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;
using Neo.Wallets;
using System.Collections.Generic;
using NeoExpress.Abstractions;

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

        private static JArray GetGenesisSignatures(ExpressChain chain, IEnumerable<UInt160> hashes, byte[] data)
        {
            var signatures = new JArray();
            //foreach (var sig in chain.ConsensusNodes.SelectMany(n => n.Wallet.Sign(hashes, data)))
            //{
            //    signatures.Add(sig);
            //}
            return signatures;
        }

        //private static JArray GetStandardSignatures(ExpressWalletAccount account, IEnumerable<UInt160> hashes, byte[] data) => new JArray(account.Sign(data));

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
                //var result = await NeoRpcClient.ExpressTransfer(uri, Asset, Quantity, senderAccount.ScriptHash, receiverAccount.ScriptHash)
                //    .ConfigureAwait(false);
                //console.WriteLine(result.ToString(Formatting.Indented));

                //var txid = result["txid"];
                //if (txid != null)
                //{
                //    console.WriteLine("transfer complete");
                //}
                //else
                //{
                //    var (hashes, data) = NeoUtility.ParseResultHashesAndData(result);
                //    var signatures = DevChain.IsGenesis(Sender)
                //        ? GetGenesisSignatures(devChain, hashes, data)
                //        : GetStandardSignatures(senderAccount, hashes, data);

                //    var result2 = await NeoRpcClient.ExpressSubmitSignatures(uri, result["contract-context"], signatures);
                //    console.WriteLine(result2.ToString(Formatting.Indented));
                //}

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
