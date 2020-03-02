using McMaster.Extensions.CommandLineUtils;
using Neo.Network.P2P.Payloads;
using NeoExpress.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
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
                var account = chain.GetAccount(Account);
                if (account == null)
                {
                    throw new Exception($"{Account} account not found.");
                }

                var uri = chain.GetUri();
                var claimable = (await NeoRpcClient.GetClaimable(uri, account.ScriptHash)
                    .ConfigureAwait(false))?.ToObject<ClaimableResponse>();
                if (claimable == null)
                {
                    throw new Exception($"could not retrieve claimable for {Account}");
                }

                var tx = RpcTransactionManager.CreateClaimTransaction(account, claimable);
                tx.Witnesses = new[] { RpcTransactionManager.GetWitness(tx, chain, account) };
                var sendResult = await NeoRpcClient.SendRawTransaction(uri, tx);
                if (sendResult == null || !sendResult.Value<bool>())
                {
                    throw new Exception("SendRawTransaction failed");
                }

                console.WriteLine($"Claim Transaction {tx.Hash} submitted");
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
