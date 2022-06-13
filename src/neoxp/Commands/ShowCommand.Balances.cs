using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Newtonsoft.Json;

namespace NeoExpress.Commands
{
    partial class ShowCommand
    {
        [Command("balances", Description = "Show all NEP-17 asset balances for an account")]
        internal class Balances
        {
            readonly IExpressChain chain;

            public Balances(IExpressChain chain)
            {
                this.chain = chain;
            }

            public Balances(CommandLineApplication app)
            {
                this.chain = app.GetExpressFile();
            }

            [Argument(0, Description = "Account to show asset balances for")]
            [Required]
            internal string Account { get; init; } = string.Empty;

            [Option(Description = "Output as JSON")]
            internal bool Json { get; init; } = false;

            internal Task<int> OnExecuteAsync(CommandLineApplication app) => app.ExecuteAsync(this.ExecuteAsync);

            internal async Task ExecuteAsync(IConsole console)
            {
                using var expressNode = chain.GetExpressNode();
                var accountHash = expressNode.Chain.ResolveAccountHash(Account);
                var balances = await expressNode.ListBalancesAsync(accountHash).ConfigureAwait(false);

                if (Json)
                {
                    using var writer = new JsonTextWriter(console.Out) { Formatting = Formatting.Indented };
                    using var _ = writer.WriteArray();

                    for (int i = 0; i < balances.Count; i++)
                    {
                        var balance = new BigDecimal(balances[i].balance, balances[i].contract.Decimals);
                        
                        using var __ = writer.WriteObject();
                        writer.WriteProperty("symbol", $"{balances[i].contract.Symbol}");
                        writer.WriteProperty("contractHash", $"{balances[i].contract.ScriptHash}");
                        writer.WriteProperty("balance", $"{balance}");
                    }
                }
                else
                {
                    if (balances.Count == 0)
                    {
                        console.WriteLine($"No balances for {Account}");
                    }

                    for (int i = 0; i < balances.Count; i++)
                    {
                        console.WriteLine($"{balances[i].contract.Symbol} ({balances[i].contract.ScriptHash})");
                        console.WriteLine($"  balance: {new BigDecimal(balances[i].balance, balances[i].contract.Decimals)}");
                    }
                }
            }
        }
    }
}
