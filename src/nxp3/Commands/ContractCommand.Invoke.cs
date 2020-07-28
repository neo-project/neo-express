using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Neo3;

namespace nxp3.Commands
{
    partial class ContractCommand
    {
        [Command(Name = "invoke")]
        class Invoke
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
                        throw new Exception($"Invocation file {InvocationFile} couldn't be found");
                    }

                    var (chain, _) = Program.LoadExpressChain(Input);
                    var blockchainOperations = new BlockchainOperations();

                    if (Test)
                    {
                        var result = await blockchainOperations.TestInvokeContract(chain, InvocationFile);
                        console.WriteLine($"Tx: {result.Tx}");
                        console.WriteLine($"Gas Consumed: {result.GasConsumed}");
                        console.WriteLine("Result Stack:");
                        foreach (var v in result.Stack)
                        {
                            console.WriteLine($"\t{v}");
                        }
                    }
                    else
                    {
                        var account = blockchainOperations.GetAccount(chain, Account);
                        if (account == null)
                        {
                            throw new Exception($"{Account} account not found.");
                        }
                        var txHash = await blockchainOperations.InvokeContract(chain, InvocationFile, account);
                        console.WriteLine($"InvocationTransaction {txHash} submitted");
                    }
                    return 0;
                }
                catch (Exception ex)
                {
                    console.WriteLine(ex.Message);
                    app.ShowHelp();
                    return 1;
                }
            }

        }
    }
}
