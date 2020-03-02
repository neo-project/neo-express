using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NeoExpress.Commands
{

    internal partial class ContractCommand
    {
        [Command(Name = "deploy")]
        private class Deploy
        {
            [Argument(0)]
            [Required]
            string Contract { get; } = string.Empty;

            [Argument(1)]
            [Required]
            string Account { get; } = string.Empty;

            [Option]
            private string Input { get; } = string.Empty;

            [Option]
            private string Name { get; } = string.Empty;

            [Option]
            private bool Overwrite { get; }

            private static async Task<(bool deployed, JToken? result)> GetContractState(Uri uri, ExpressContract contract)
            {
                try
                {
                    var result = await NeoRpcClient.GetContractState(uri, contract.Hash).ConfigureAwait(false);
                    if (result != null)
                    {
                        return (true, result);
                    }

                    return (false, null);
                }
                catch (Exception)
                {
                    return (false, null);
                }
            }

            private static (bool storage, bool dynamicInvoke) GetContractProps(JToken result)
            {
                var properties = result["properties"];
                return (storage: properties.Value<bool>("storage"),
                    dynamicInvoke: properties.Value<bool>("dynamic_invoke"));
            }

            private async Task<ExpressContract> DeployContract(ExpressChain chain, ExpressContract contract, Uri uri, IConsole console)
            {
                var account = chain.GetAccount(Account);
                if (account == null)
                {
                    throw new Exception($"Account {Account} not found.");
                }

                if (!contract.Properties.ContainsKey("has-storage"))
                {
                    var hasStorage = Prompt.GetYesNo("Does this contract use storage?", false);
                    contract.Properties.Add("has-storage", hasStorage.ToString());
                }

                if (!contract.Properties.ContainsKey("has-dynamic-invoke"))
                {
                    var hasStorage = Prompt.GetYesNo("Does this contract use dynamic invoke?", false);
                    contract.Properties.Add("has-dynamic-invoke", hasStorage.ToString());
                }

                var unspents = (await NeoRpcClient.GetUnspents(uri, account.ScriptHash)
                    .ConfigureAwait(false))?.ToObject<UnspentsResponse>();
                if (unspents == null)
                {
                    throw new Exception($"could not retrieve unspents for {Account}");
                }

                var tx = RpcTransactionManager.CreateDeploymentTransaction(contract, 
                    account, unspents);
                tx.Witnesses = new[] { RpcTransactionManager.GetWitness(tx, chain, account) };
                var sendResult = await NeoRpcClient.SendRawTransaction(uri, tx);
                if (sendResult == null || !sendResult.Value<bool>())
                {
                    throw new Exception("SendRawTransaction failed");
                }

                console.WriteLine($"Contract Deployment {tx.Hash} submitted");
                return contract;
            }

            async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, filename) = Program.LoadExpressChain(Input);
                    var contract = chain.GetContract(Contract);

                    if (chain.Contracts == null)
                    {
                        chain.Contracts = new List<ExpressContract>(1);
                    }

                    if (!string.IsNullOrEmpty(Name))
                    {
                        contract.Name = Name;
                    }

                    var uri = chain.GetUri();
                    var (deployed, result) = await GetContractState(uri, contract).ConfigureAwait(false);

                    if (deployed)
                    {
                        Debug.Assert(result != null);
                        console.WriteLine($"Contract matching {contract.Name} script hash already deployed.");
                        var (storage, dynamicInvoke) = GetContractProps(result);
                        contract.Properties["has-storage"] = storage.ToString();
                        contract.Properties["has-dynamic-invoke"] = dynamicInvoke.ToString();
                    }
                    else
                    {
                        console.WriteLine($"Deploying {contract.Name} contract.");
                        contract = await DeployContract(chain, contract, uri, console);
                    }

                    chain.SaveContract(contract, filename, console, Overwrite);
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
}
