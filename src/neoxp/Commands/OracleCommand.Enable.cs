using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    partial class OracleCommand
    {
        [Command("enable", Description = "Enable oracles for neo-express instance")]
        internal class Enable
        {
            readonly IExpressChainManagerFactory chainManagerFactory;

            public Enable(IExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, Description = "Account to pay contract invocation GAS fee")]
            [Required]
            string Account { get; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            [Option(Description = "Output as JSON")]
            bool Json { get; } = false;

            internal async Task<int> OnExecuteAsync(IConsole console)
            {
                try
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    using var expressNode = chainManager.GetExpressNode();
                    await ExecuteAsync(chainManager, expressNode, Account, console.Out, Json);
                    return 0;
                }
                catch (Exception ex)
                {
                    await console.Error.WriteLineAsync(ex.Message);
                    return 1;
                }
            }

            internal static async Task ExecuteAsync(IExpressChainManager chainManager, IExpressNode expressNode, string account, TextWriter writer, bool json = false)
            {
                var _account = chainManager.Chain.GetAccount(account) ?? throw new Exception($"{account} account not found.");
                var oracles = chainManager.Chain.ConsensusNodes.Select(n => n.Wallet.DefaultAccount ?? throw new Exception());
                var txHash = await expressNode.DesignateOracleRolesAsync(_account, oracles);
                if (json)
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
