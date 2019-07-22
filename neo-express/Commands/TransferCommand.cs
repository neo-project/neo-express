using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Neo.Cryptography;
using Neo.SmartContract;
using Neo.Wallets;
using System.Collections.Generic;

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

        private async Task<int> GenesisTransferAsync(CommandLineApplication app, IConsole console, DevChain devchain)
        {
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

            var (hashes, data) = NeoUtility.ParseResultHashesAndData(result);
            var signatures = new JArray();
            foreach (var sig in devchain.ConsensusNodes.SelectMany(n => n.Wallet.Sign(hashes, data)))
            {
                signatures.Add(sig);
            }

            console.WriteLine(signatures.ToString(Formatting.Indented));
            var result2 = await NeoRpcClient.ExpressSubmitSignatures(uri, result["contract-context"], signatures);
            console.WriteLine(result2.ToString(Formatting.Indented));

            return 0;
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

            if (DevChain.IsGenesis(Sender))
            {
                return await GenesisTransferAsync(app, console, devchain);
            }

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
                var (_, data) = NeoUtility.ParseResultHashesAndData(result);
                var signatures = new JArray(senderAccount.Sign(data));
                var result2 = await NeoRpcClient.ExpressSubmitSignatures(uri, result["contract-context"], signatures);
                console.WriteLine(result2.ToString(Formatting.Indented));
            }

            return 0;
        }
    }
}
