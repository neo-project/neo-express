using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Abstractions;
using NeoExpress.Neo2;
using System;
using System.Threading.Tasks;

namespace NeoExpress.Commands
{
    [Command("claim")]

    class ClaimCommand
    {
        [Argument(0)]
        private string Asset { get; } = string.Empty;

        [Argument(1)]
        private string Account { get; } = string.Empty;

        [Option]
        private string Input { get; } = string.Empty;

        private async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
        {
            try
            {
                if (!"gas".Equals(Asset, StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception("Only GAS can be claimed");
                }
                
                var (chain, _) = Program.LoadExpressChain(Input);
                var account = chain.GetAccount(Account);
                if (account == null)
                {
                    throw new Exception($"{Account} account not found.");
                }

                var blockchainOperations = new BlockchainOperations();
                var tx = await blockchainOperations.Claim(chain, account);

                console.WriteLine($"Claim Transaction {tx.Hash} submitted");
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
