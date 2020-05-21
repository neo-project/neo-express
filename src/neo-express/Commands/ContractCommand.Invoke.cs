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
        [Command(Name = "invoke")]
        private class Invoke
        {
            [Argument(0)]
            [Required]
            string InvocationFile { get; } = string.Empty;

            [Argument(1)]
            string Account { get; } = string.Empty;

            [Option]
            bool Test { get; } = false;

            [Option]
            string Input { get; } = string.Empty;

            async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    if (!File.Exists(InvocationFile))
                    {
                        console.WriteError($"Invocation file {InvocationFile} couldn't be found");
                        console.WriteWarning("    Note: The arguments for the contract invoke command changed significantly in the v1.1 release.");
                        console.WriteWarning("          Please see https://neo-project.github.io/neo-express/command-reference for details.\n");

                        app.ShowHelp();
                        return 1;
                    }

                    var (chain, _) = Program.LoadExpressChain(Input);

                    if (Test)
                    {
                        var result = await BlockchainOperations.TestInvokeContract(chain, InvocationFile);

                        console.WriteLine($"Tx: {result.Tx}");
                        console.WriteLine($"Gas Consumed: {result.GasConsumed}");
                        console.WriteLine("Result Stack:");
                        foreach (var v in result.ReturnStack)
                        {
                            console.WriteLine($"\t{v.Value} ({v.Type})");
                        }
                    }
                    else
                    {
                        var account = chain.GetAccount(Account);
                        if (account == null)
                        {
                            throw new Exception("Invalid Account");
                        }
                        
                        var tx = await BlockchainOperations.InvokeContract(chain, InvocationFile, account);
                        console.WriteLine($"InvocationTransaction {tx.Hash} submitted");
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
