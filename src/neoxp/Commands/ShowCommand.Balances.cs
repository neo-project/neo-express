using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;

namespace NeoExpress.Commands
{
    partial class ShowCommand
    {
        [Command("balances", Description = "Show all NEP-17 asset balances for an account")]
        internal class Balances
        {
            readonly ExpressChainManagerFactory chainManagerFactory;

            public Balances(ExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, Description = "Account to show asset balances for")]
            [Required]
            internal string Account { get; init; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    if (!chainManager.Chain.TryGetAccountHash(Account, out var accountHash))
                    {
                        throw new Exception($"{Account} account not found.");
                    }

                    using var expressNode = chainManager.GetExpressNode();
                    var balances = await expressNode.ListBalancesAsync(accountHash).ConfigureAwait(false);

                    if (balances.Count == 0)
                    {
                        console.WriteLine($"No balances for {Account}");
                    }

                    for (int i = 0; i < balances.Count; i++)
                    {
                        console.WriteLine($"{balances[i].contract.Symbol} ({balances[i].contract.ScriptHash})");
                        console.WriteLine($"  balance: {new BigDecimal(balances[i].balance, balances[i].contract.Decimals)}");
                    }

                    return 0;
                }
                catch (Exception ex)
                {
                    app.WriteException(ex);
                    return 1;
                }
            }
        }
    }
}
