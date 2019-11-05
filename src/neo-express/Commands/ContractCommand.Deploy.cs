using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Abstractions.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
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

            async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, filename) = Program.LoadExpressChain(Input);

                    var contract = chain.GetContract(Contract);
                    if (contract == null)
                    {
                        contract = Program.BlockchainOperations.LoadContract(Contract, (prompt, @default) => Prompt.GetYesNo(prompt, @default));
                    }

                    if (!string.IsNullOrEmpty(Name))
                    {
                        contract.Name = Name;
                    }

                    var uri = chain.GetUri();
                    var (deployed, getContractStateResult) = await GetContractState(uri, contract).ConfigureAwait(false);

                    if (deployed)
                    {
                        Debug.Assert(getContractStateResult != null);
                        console.WriteLine($"Contract matching {contract.Name} script hash already deployed.");

                        // I have a sneaking suspicion these will have to change with Neo3, but leaving in for now
                        var (storage, dynamicInvoke) = GetContractProps(getContractStateResult);
                        contract.Properties["has-storage"] = storage.ToString();
                        contract.Properties["has-dynamic-invoke"] = dynamicInvoke.ToString();
                    }
                    else
                    {
                        var account = chain.GetAccount(Account);
                        if (account == null)
                        {
                            throw new Exception($"Account {Account} not found.");
                        }

                        console.WriteLine($"Deploying {contract.Name} contract.");
                        var results = await Program.BlockchainOperations.DeployContract(chain, contract, account).ConfigureAwait(false);
                        foreach (var result in results)
                        {
                            console.WriteResult(result);
                        }
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
