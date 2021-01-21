using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using NeoExpress.Abstractions.Models;


namespace NeoExpress.Commands
{
    partial class ShowCommand
    {
        [Command("balances")]
        class Balances
        {
            [Argument(0, Description = "Account to show asset balances for")]
            [Required]
            string Account { get; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
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

                    if (balances.Length == 0)
                    {
                        console.WriteLine($"No balances for {Account}");
                    }

                    for (int i = 0; i < balances.Length; i++)
                    {
                        var balance = new BigDecimal(balances[i].balance.Amount, balances[i].contract.Decimals);
                        console.WriteLine($"{balances[i].contract.Symbol} ({balances[i].contract.ScriptHash})");
                        console.WriteLine($"  balance: {balance}");
                    }
                    return 0;
                }
                catch (Exception ex)
                {
                    await console.Error.WriteLineAsync(ex.Message);
                    return 1;
                }
            }
        }
    }
}
