using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
            string Contract { get; }

            [Argument(1)]
            [Required]
            string Account { get; }

            [Option]
            private string Input { get; }

            [Option]
            private string Name { get; }

            enum DeployedStatus
            {
                Unknown,
                Deployed,
                NotDeployed
            }

            private static async Task<(DeployedStatus, JToken)> GetContractState(Uri uri, ExpressContract contract)
            {
                try
                {
                    var result = await NeoRpcClient.GetContractState(uri, contract.Hash).ConfigureAwait(false);
                    if (result != null)
                    {
                        return (DeployedStatus.Deployed, result);
                    }

                    return (DeployedStatus.NotDeployed, null);
                }
                catch (Exception)
                {
                    return (DeployedStatus.Unknown, null);
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
                if (account == default)
                {
                    throw new Exception($"Account {Account} not found.");
                }

                if (Prompt.GetYesNo("Does this contract use storage?", false))
                {
                    contract.Properties.Add("has-storage", "true");
                }

                if (Prompt.GetYesNo("Does this contract use dynamic invoke?", false))
                {
                    contract.Properties.Add("has-dynamic-invoke", "true");
                }

                var result = await NeoRpcClient.ExpressDeployContract(uri, contract, account.ScriptHash).ConfigureAwait(false);
                console.WriteLine(result.ToString(Formatting.Indented));

                var txid = result["txid"];
                if (txid != null)
                {
                    console.WriteLine("deployment complete");
                }
                else
                {
                    var signatures = account.Sign(chain.ConsensusNodes, result);
                    var result2 = await NeoRpcClient.ExpressSubmitSignatures(uri, result["contract-context"], signatures).ConfigureAwait(false);
                    console.WriteLine(result2.ToString(Formatting.Indented));
                }

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
                    var (state, result) = await GetContractState(uri, contract).ConfigureAwait(false);

                    if (state == DeployedStatus.Deployed)
                    {
                        console.WriteLine($"Contract matching {contract.Name} script hash already deployed.");
                        var (storage, dynamicInvoke) = GetContractProps(result);
                        contract.Properties["has-storage"] = storage.ToString();
                        contract.Properties["has-dynamic-invoke"] = dynamicInvoke.ToString();
                    }
                    else if (state == DeployedStatus.NotDeployed)
                    {
                        console.WriteLine($"Deploying {contract.Name} contract.");
                        contract = await DeployContract(chain, contract, uri, console);
                    }

                    for (var i = chain.Contracts.Count - 1; i >= 0; i--)
                    {
                        var c = chain.Contracts[i];
                        if (string.Equals(contract.Hash, c.Hash))
                        {
                            chain.Contracts.RemoveAt(i);
                        }
                        else if (string.Equals(contract.Name, c.Name, StringComparison.InvariantCultureIgnoreCase))
                        {
                            console.WriteWarning($"Contract named {c.Name} already exists. Cannot save contract info to .neo-express.json file");
                            return 1;
                        }
                    }

                    chain.Contracts.Add(contract);
                    chain.Save(filename);
                    console.WriteLine($"Contract {contract.Name} info saved to .neo-express.json file");
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
