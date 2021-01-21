using System;
using System.Linq;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Abstractions.Models;

namespace nxp3.Commands
{
    partial class WalletCommand
    {
        [Command("list", Description = "List neo-express wallets")]
        class List
        {
            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            internal int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, filename) = Program.LoadExpressChain(Input);
                    var blockchainOperations = new NeoExpress.Neo3.BlockchainOperations();

                    var wallets = chain.ConsensusNodes.Select(n => n.Wallet)
                        .Concat(chain.Wallets ?? Enumerable.Empty<ExpressWallet>());
                    foreach (var wallet in wallets)
                    {
                        blockchainOperations.PrintWalletInfo(wallet, console.Out);
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
}
