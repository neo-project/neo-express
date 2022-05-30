using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.VM;

namespace NeoExpress.Commands
{
    partial class ShowCommand
    {
        [Command("balance", Description = "Show asset balance for account")]
        internal class Balance
        {
            readonly IExpressChain chain;

            public Balance(IExpressChain chain)
            {
                this.chain = chain;
            }

            public Balance(CommandLineApplication app) : this(app.GetExpressFile())
            {
            }

            [Argument(0, Description = "Asset to show balance of (symbol or script hash)")]
            [Required]
            internal string Asset { get; init; } = string.Empty;

            [Argument(1, Description = "Account to show asset balance for")]
            [Required]
            internal string Account { get; init; } = string.Empty;

            internal Task<int> OnExecuteAsync(CommandLineApplication app) => app.ExecuteAsync(this.ExecuteAsync);

            internal async Task ExecuteAsync(IConsole console)
            {
                using var expressNode = chain.GetExpressNode();
                var accountHash = expressNode.Chain.ResolveAccountHash(Account);
                var assetHash = await expressNode.ParseAssetAsync(Asset).ConfigureAwait(false);

                var builder = new ScriptBuilder();
                builder.EmitDynamicCall(assetHash, "balanceOf", accountHash);
                builder.EmitDynamicCall(assetHash, "symbol");
                builder.EmitDynamicCall(assetHash, "decimals");

                var result = await expressNode.GetResultAsync(builder).ConfigureAwait(false);
                var balanceOf = result.Stack[0].GetInteger();
                var symbol = result.Stack[1].GetString();
                var decimals = (byte)result.Stack[2].GetInteger();
                
                var balance = new BigDecimal(balanceOf, decimals);

                await console.Out.WriteLineAsync($"{symbol} ({assetHash}) balance: {balance}");
            }
        }
    }
}
