using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using NeoExpress.Abstractions.Models;
using NeoExpress.Neo3;

namespace nxp3.Commands
{
    partial class ShowCommand
    {
        [Command("balances")]
        class Balances
        {
            [Argument(0)]
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

                    var balances = await blockchainOperations.GetBalances(chain, account);

                    for (int i = 0; i < balances.Length; i++)
                    {
                        var balance = new BigDecimal(balances[i].balance.Amount, balances[i].contract.Decimals);
                        console.WriteLine($"{balances[i].contract.Symbol} ({balances[i].contract.ScriptHash})\n  balance: {balance}");
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
