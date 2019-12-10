using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace NeoExpress.Commands
{
    [Command("show")]
    [Subcommand(typeof(Account), typeof(Claimable), typeof(Coins), typeof(Gas), typeof(Unspents))]
    class ShowCommand
    {
        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp();
            return 1;
        }

        private static async Task<int> ExecuteAsync(IConsole console, string name, string input, Func<Uri, string, Task<JToken?>> func, bool json = true, Action<JToken>? writeResponse = null)
        {
            try
            {
                var (chain, _) = Program.LoadExpressChain(input);
                var account = chain.GetAccount(name);
                if (account == null)
                {
                    throw new Exception($"{name} wallet not found.");
                }

                var uri = chain.GetUri();
                var response = await func(uri, account.ScriptHash).ConfigureAwait(false);
                if (response == null)
                {
                    throw new ApplicationException("no response from RPC server");
                }

                if (json || writeResponse == null)
                {
                    console.WriteResult(response);
                }
                else
                {
                    writeResponse(response);
                }

                return 0;
            }
            catch (Exception ex)
            {
                console.WriteError(ex.Message);
                return 1;
            }
        }

        [Command("account", Description = "show account state", ExtendedHelpText = @"
Remarks:
  For more info, please see https://docs.neo.org/docs/en-us/reference/rpc/latest-version/api/getaccountstate.html")]
        private class Account
        {
            [Argument(0)]
            [Required]
            private string Name { get; } = string.Empty;

            [Option]
            private string Input { get; } = string.Empty;

            [Option]
            private bool Json { get; }

            private Task<int> OnExecuteAsync(IConsole console)
            {
                void WriteResponse(JToken token)
                {
                    var response = token.ToObject<AccountResponse>();
                    console.WriteLine($"Account information for {Name}:");
                    foreach (var balance in response.Balances)
                    {
                        console.WriteLine($"  Asset {balance.Asset}: {balance.Value}");
                    }
                }

                return ExecuteAsync(console, Name, Input, NeoRpcClient.GetAccountState, Json, WriteResponse);
            }
        }

        [Command("claimable", Description = "Show claimable GAS")]
        private class Claimable
        {
            [Argument(0)]
            [Required]
            private string Name { get; } = string.Empty;

            [Option]
            private string Input { get; } = string.Empty;

            [Option]
            private bool Json { get; }

            private Task<int> OnExecuteAsync(IConsole console)
            {
                void WriteResponse(JToken token)
                {
                    var response = token.ToObject<ClaimableResponse>();
                    console.WriteLine($"Claimable GAS for {Name}: {response.Unclaimed}");
                    foreach (var tx in response.Transactions)
                    {
                        console.WriteLine($"  transaction {tx.TransactionId}({tx.Index}): {tx.Unclaimed}");
                    }
                }

                return ExecuteAsync(console, Name, Input, NeoRpcClient.GetClaimable, Json, WriteResponse);
            }
        }

        [Command("coins", Description = "show all UTXO asset transactions for an account")]
        private class Coins
        {
            [Argument(0)]
            [Required]
            private string Name { get; } = string.Empty;

            [Option]
            private string Input { get; } = string.Empty;

            private Task<int> OnExecuteAsync(IConsole console)
            {
                return ExecuteAsync(console, Name, Input, NeoRpcClient.ExpressShowCoins);
            }
        }

        [Command("gas", "unclaimed", Description = "Show unclaimed GAS (alias \"unclaimed\")", ExtendedHelpText = @"
Remarks:
  Unavailable GAS can be converted to available GAS by transfering NEO.
  For more info, please see https://github.com/neo-project/neo-express/blob/master/docs/command-reference.md#neo-express-claim")]
        private class Gas
        {
            [Argument(0)]
            [Required]
            private string Name { get; } = string.Empty;

            [Option]
            private string Input { get; } = string.Empty;

            [Option]
            private bool Json { get; }

            private Task<int> OnExecuteAsync(IConsole console)
            {
                void WriteResponse(JToken token)
                {
                    var response = token.ToObject<UnclaimedResponse>();
                    console.WriteLine($"Unclaimed GAS for {Name}: {response.Unclaimed}");
                    console.WriteLine($"    Available GAS: {response.Available}");
                    console.WriteLine($"  Unavailable GAS: {response.Unavailable}");
                }

                return ExecuteAsync(console, Name, Input, NeoRpcClient.GetUnclaimed, Json, WriteResponse);
            }
        }

        [Command("unspents", "unspent", Description = "Show unspent assets (alias \"unspent\")")]
        private class Unspents
        {
            [Argument(0)]
            [Required]
            private string Name { get; } = string.Empty;

            [Option]
            private string Input { get; } = string.Empty;

            [Option]
            private bool Json { get; }

            private Task<int> OnExecuteAsync(IConsole console)
            {
                void WriteResponse(JToken token)
                {
                    var response = token.ToObject<UnspentsResponse>();
                    console.WriteLine($"Unspent assets for {Name}");
                    foreach (var balance in response.Balance)
                    {
                        console.WriteLine($"  {balance.AssetSymbol}: {balance.Amount}");
                        console.WriteLine($"    asset hash: {balance.AssetHash}");
                        console.WriteLine("    transactions:");
                        foreach (var tx in balance.Transactions)
                        {
                            console.WriteLine($"      {tx.TransactionId}({tx.Index}): {tx.Value}");
                        }
                    }
                }

                return ExecuteAsync(console, Name, Input, NeoRpcClient.GetUnspents, Json, WriteResponse);
            }
        }
    }
}
