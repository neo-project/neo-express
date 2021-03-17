using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Models;

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
            internal string Account { get; init; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            [Option(Description = "Output as JSON")]
            internal bool Json { get; init; } = false;

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

            internal static async Task ExecuteAsync(IExpressChainManager chainManager, IExpressNode expressNode, string accountName, TextWriter writer, bool json = false)
            {
                if (!chainManager.Chain.TryGetAccount(accountName, out var wallet, out var account, chainManager.ProtocolSettings))
                {
                    throw new Exception($"{accountName} account not found.");
                }

                var oracles = chainManager.Chain.ConsensusNodes
                    .Select(n => DevWalletAccount.FromExpressWalletAccount(chainManager.ProtocolSettings, n.Wallet.DefaultAccount ?? throw new Exception()))
                    .Select(a => a.GetKey()?.PublicKey ?? throw new Exception());
                var txHash = await expressNode.DesignateOracleRolesAsync(wallet, account.ScriptHash, oracles).ConfigureAwait(false);
                await writer.WriteTxHashAsync(txHash, "Oracle Enable", json).ConfigureAwait(false);
            }
        }
    }
}
