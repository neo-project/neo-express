using McMaster.Extensions.CommandLineUtils;
using System;
using System.Threading.Tasks;

namespace NeoExpress.Commands
{
    [Command("claim")]

    class ClaimCommand
    {
        [Argument(0)]
        private string Asset { get; } = string.Empty;

        [Argument(1)]
        private string Account { get; } = string.Empty;

        [Option]
        private string Input { get; } = string.Empty;

        private async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
        {
            try
            {
                var (chain, _) = Program.LoadExpressChain(Input);
                //var account = chain.GetAccount(Account);
                //if (account == null)
                //{
                //    throw new Exception($"{Account} account not found.");
                //}

                //var uri = chain.GetUri();
                //var result = await NeoRpcClient.ExpressClaim(uri, Asset, account.ScriptHash)
                //    .ConfigureAwait(false);
                //console.WriteResult(result);

                //var txid = result?["txid"];
                //if (txid != null)
                //{
                //    console.WriteLine("transfer complete");
                //}
                //else
                //{
                //    var signatures = account.Sign(chain.ConsensusNodes, result);
                //    var result2 = await NeoRpcClient.ExpressSubmitSignatures(uri, result?["contract-context"], signatures).ConfigureAwait(false);
                //    console.WriteResult(result2);
                //}

                return 0;
            }
            catch (Exception ex)
            {
                console.WriteError(ex.Message);
                app.ShowHelp();
                return 1;
            }
        }
    }
}
