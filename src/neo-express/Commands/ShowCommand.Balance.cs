using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;


namespace NeoExpress.Commands
{
    partial class ShowCommand
    {
        [Command("balance", Description = "Show asset balance for account")]
        class Balance
        {
            [Argument(0, Description = "Asset to show balance of (symbol or script hash)")]
            [Required]
            string Asset { get; } = string.Empty;

            [Argument(1, Description = "Account to show asset balance for")]
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

                    var (balance, contract) = await blockchainOperations.ShowBalance(chain, account, Asset);

                    console.WriteLine($"{contract.Symbol} ({contract.ScriptHash})\n  balance: {balance}");
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
