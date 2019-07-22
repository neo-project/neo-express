using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
            var senderAddress = devchain.GetAddress(Sender);
            if (senderAddress == default)
            {
                console.WriteLine($"{Sender} sender not found.");
                app.ShowHelp();
                return 1;
            }

            var receiverAddress = devchain.GetAddress(Receiver);
            if (receiverAddress == default)
            {
                console.WriteLine($"{Receiver} receiver not found.");
                app.ShowHelp();
                return 1;
            }

            var uri = new Uri($"http://localhost:{devchain.ConsensusNodes.First().RpcPort}");
            var result = await NeoRpcClient.ExpressTransfer(uri, Asset, Quantity, senderAddress, receiverAddress);
            console.WriteLine(result.ToString(Formatting.Indented));
            return 0;
        }
    }
}
