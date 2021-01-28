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
            readonly IExpressChainManagerFactory chainManagerFactory;

            public Balance(IExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, Description = "Asset to show balance of (symbol or script hash)")]
            [Required]
            string Asset { get; } = string.Empty;

            [Argument(1, Description = "Account to show asset balance for")]
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
                    var (balance, contract) = await expressNode.GetBalanceAsync(account, Asset).ConfigureAwait(false);
                    await console.Out.WriteLineAsync($"{contract.Symbol} ({contract.ScriptHash})\n  balance: {balance.ToBigDecimal(contract.Decimals)}");
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
