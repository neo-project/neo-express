using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Neo;
using Neo.Cryptography;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.Wallets;
using Neo.Wallets.NEP6;

namespace Neo.Express.Commands
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

        static JArray SignContext(JToken ctx, DevWalletAccount senderAccount)
        {
            var hashes = ctx["script-hashes"].Select(t => t.Value<string>().ToScriptHash());
            var data = ctx.Value<string>("hash-data").HexToBytes();
            var signatures = new JArray();

            foreach (var hash in hashes)
            {
                if (senderAccount?.HasKey != true) continue;
                var key = senderAccount.GetKey();
                var signature = Crypto.Default.Sign(data, key.PrivateKey,
                    key.PublicKey.EncodePoint(false).Skip(1).ToArray());

                signatures.Add(new JObject
                {
                    ["signature"] = signature.ToHexString(),
                    ["public-key"] = key.PublicKey.EncodePoint(true).ToHexString(),
                    ["contract"] = new JObject
                    {
                        ["script"] = senderAccount.Contract.Script.ToHexString(),
                        ["parameters"] = new JArray(senderAccount.Contract.ParameterList.Select(cpt => Enum.GetName(typeof(Neo.SmartContract.ContractParameterType), cpt)))
                    }
                });
            }

            return signatures;
        }

        private async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
        {
            var input = Program.DefaultPrivatenetFileName(Input);
            if (!File.Exists(input))
            {
                console.WriteLine($"{input} doesn't exist");
                app.ShowHelp();
                return 1;
            }

            var devchain = DevChain.Load(input);

            var senderAccount = devchain.GetAccount(Sender);
            if (senderAccount == default)
            {
                console.WriteLine($"{Sender} sender not found.");
                app.ShowHelp();
                return 1;
            }

            var receiverAccount = devchain.GetAccount(Receiver);
            if (receiverAccount == default)
            {
                console.WriteLine($"{Receiver} receiver not found.");
                app.ShowHelp();
                return 1;
            }

            var uri = devchain.GetUri();
            var result = await NeoRpcClient.ExpressTransfer(uri, Asset, Quantity, senderAccount.ScriptHash, receiverAccount.ScriptHash);
            console.WriteLine(result.ToString(Formatting.Indented));

            var txid = result["txid"];
            if (txid != null)
            {
                console.WriteLine("transfer complete");
            }
            else
            {
                var signatures = SignContext(result, senderAccount);
                var result2 = await NeoRpcClient.ExpressSubmitSignatures(uri, result["contract-context"], signatures);
                console.WriteLine(result2.ToString(Formatting.Indented));
            }

            return 0;
        }
    }
}
