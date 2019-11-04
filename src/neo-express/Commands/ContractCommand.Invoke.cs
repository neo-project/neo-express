using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Abstractions.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace NeoExpress.Commands
{
    internal partial class ContractCommand
    {
        [Command(Name = "invoke")]
        private class Invoke
        {
            [Argument(0)]
            [Required]
            string Contract { get; } = string.Empty;

            [Argument(1)]
            string[] Arguments { get; } = Array.Empty<string>();

            [Option]
            private string Input { get; } = string.Empty;

            [Option]
            private string Account { get; } = string.Empty;

            [Option]
            private string Function { get; } = string.Empty;

            static IEnumerable<JObject> ParseArguments(ExpressContract.Function function, IEnumerable<string> arguments)
            {
                if (function.Parameters.Count != arguments.Count())
                {
                    throw new ApplicationException($"Invalid number of arguments. Expecting {function.Parameters.Count} received {arguments.Count()}");
                }

                return function.Parameters.Zip(arguments,
                    (param, argValue) => new JObject()
                    {
                        ["type"] = param.Type,
                        ["value"] = argValue
                    });
            }

            IEnumerable<JObject> ParseArguments(ExpressContract contract)
            {
                var arguments = Arguments ?? Enumerable.Empty<string>();

                if (string.IsNullOrEmpty(Function))
                {
                    var entrypoint = contract.Functions.Single(f => f.Name == contract.EntryPoint);
                    return ParseArguments(entrypoint, arguments);
                }
                else
                {
                    var function = contract.Functions.SingleOrDefault(f => f.Name == Function);

                    if (function == null)
                    {
                        throw new Exception($"Could not find function {Function}");
                    }

                    return new JObject[2]
                    {
                        new JObject()
                        {
                            ["type"] = "String",
                            ["value"] = Function
                        },
                        new JObject()
                        {
                            ["type"] = "Array",
                            ["value"] = new JArray(ParseArguments(function, arguments))
                        }
                    };
                }
            }

            async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);
                    var contract = chain.GetContract(Contract);
                    if (contract == null)
                    {
                        throw new Exception($"Contract {Contract} not found.");
                    }

                    var account = chain.GetAccount(Account);

                    var args = ParseArguments(contract);
                    var uri = chain.GetUri();
                    var result = await NeoRpcClient.ExpressInvokeContract(uri, contract.Hash, args, account?.ScriptHash);
                    console.WriteResult(result);
                    if (account != null)
                    {
                        var signatures = account.Sign(chain.ConsensusNodes, result);
                        var result2 = await NeoRpcClient.ExpressSubmitSignatures(uri, result?["contract-context"], signatures).ConfigureAwait(false);
                        console.WriteResult(result2);
                    }
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
