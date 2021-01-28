using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;


namespace NeoExpress.Commands
{
    partial class ShowCommand
    {
        [Command("balances")]
        class Balances
        {
            readonly IExpressChainManagerFactory chainManagerFactory;

            public Balances(IExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, Description = "Account to show asset balances for")]
            [Required]
            string Account { get; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            internal async Task<int> OnExecuteAsync(IConsole console)
            {
                try
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    var account = chainManager.Chain.GetAccount(Account) ?? throw new Exception($"{Account} account not found.");
                    using var expressNode = chainManager.GetExpressNode();
                    var balances = await expressNode.GetBalancesAsync(account.AsUInt160()).ConfigureAwait(false);

                    if (balances.Length == 0)
                    {
                        console.WriteLine($"No balances for {Account}");
                    }

                    for (int i = 0; i < balances.Length; i++)
                    {
                        console.WriteLine($"{balances[i].contract.Symbol} ({balances[i].contract.ScriptHash})");
                        console.WriteLine($"  balance: {balances[i].balance.ToBigDecimal(balances[i].contract.Decimals)}");
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
