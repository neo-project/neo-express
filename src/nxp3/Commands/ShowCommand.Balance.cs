using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Neo3;

namespace nxp3.Commands
{
    partial class ShowCommand
    {
        [Command("balance")]
        class Balance
        {
            [Argument(0)]
            [Required]
            string Asset { get; } = string.Empty;

            [Argument(1)]
            [Required]
            string Account { get; } = string.Empty;

            [Option]
            string Input { get; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);
                    var blockchainOperations = new BlockchainOperations();
                    var account = blockchainOperations.GetAccount(chain, Account);
                    if (account == null)
                    {
                        throw new Exception($"{Account} account not found.");
                    }

                    var (balance, contract) = await blockchainOperations.ShowBalance(chain, account, Asset);

                    console.WriteLine($"{contract.Symbol} ({contract.ScriptHash})\n  balance: {balance}");
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
