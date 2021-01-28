using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    partial class OracleCommand
    {
        [Command("enable", Description = "Enable oracles for neo-express instance")]
        class Enable
        {
            readonly IExpressChainManagerFactory chainManagerFactory;

            public Enable(IExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, Description = "Account to pay contract invocation GAS fee")]
            string Account { get; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            [Option(Description = "Output as JSON")]
            bool Json { get; } = false;

            internal async Task<int> OnExecuteAsync(IConsole console)
            {
                try
                {
                    await ExecuteAsync(console.Out);
                    return 0;
                }
                catch (Exception ex)
                {
                    await console.Error.WriteLineAsync(ex.Message);
                    return 1;
                }
            }

            internal async Task ExecuteAsync(TextWriter writer)
            {
                var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                var account = chainManager.Chain.GetAccount(Account) ?? throw new Exception($"{Account} account not found.");
                var oracles = chainManager.Chain.ConsensusNodes.Select(n => n.Wallet.DefaultAccount ?? throw new Exception());
                var expressNode = chainManager.GetExpressNode();
                var txHash = await expressNode.DesignateOracleRolesAsync(account, oracles);
                if (Json)
                {
                    await writer.WriteLineAsync($"{txHash}").ConfigureAwait(false);
                }
                else
                {
                    await writer.WriteLineAsync($"Oracle Enable Transaction {txHash} submitted").ConfigureAwait(false);
                }
            }
        }
    }
}
