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

                var result = await NeoRpcClient.ExpressDeployContract(uri, contract, account.ScriptHash).ConfigureAwait(false);
                console.WriteResult(result);

                var txid = result?["txid"];
                if (txid != null)
                {
                    console.WriteLine("deployment complete");
                }
                else
                {
                    var signatures = account.Sign(chain.ConsensusNodes, result);
                    var result2 = await NeoRpcClient.ExpressSubmitSignatures(uri, result?["contract-context"], signatures).ConfigureAwait(false);
                    console.WriteResult(result2);
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

                    for (var i = chain.Contracts.Count - 1; i >= 0; i--)
                    {
                        var c = chain.Contracts[i];
                        if (string.Equals(contract.Hash, c.Hash))
                        {
                            chain.Contracts.RemoveAt(i);
                        }
                        else if (string.Equals(contract.Name, c.Name, StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (Overwrite)
                            {
                                console.WriteWarning($"Overriting contract named {c.Name} that already exists with a different hash value.");
                                chain.Contracts.RemoveAt(i);
                            }
                            else
                            {
                                console.WriteWarning($"Contract named {c.Name} already exists with a different hash value.");
                                if (Prompt.GetYesNo("Overwrite?", false))
                                {
                                    chain.Contracts.RemoveAt(i);
                                }
                                else
                                {
                                    console.WriteWarning($"{Path.GetFileName(filename)} not updated with new {c.Name} contract info.");
                                    return 0;
                                }
                            }
                        }
                    }

                    chain.Contracts.Add(contract);
                    chain.Save(filename);
                    console.WriteLine($"Contract {contract.Name} info saved to {Path.GetFileName(filename)}");
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
