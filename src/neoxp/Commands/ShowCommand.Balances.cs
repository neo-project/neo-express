using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
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
            readonly IFileSystem fileSystem;

            public Balances(IFileSystem fileSystem)
            {
                this.fileSystem = fileSystem;
            }

            [Argument(0, Description = "Account to show asset balances for")]
            [Required]
            internal string Account { get; init; } = string.Empty;

            
            internal string Input { get; init; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = fileSystem.LoadExpressChain(Input);
                    if (!chain.TryResolveAccountHash(Account, out var accountHash))
                    {
                        throw new Exception($"{Account} account not found.");
                    }

                    using var expressNode = chain.GetExpressNode(fileSystem);
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
