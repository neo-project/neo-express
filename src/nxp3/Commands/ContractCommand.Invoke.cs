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

            [Option("--gas|-g", CommandOptionType.SingleValue, Description = "Additional GAS to apply to the contract invocation")]
            decimal AdditionalGas { get; } = 0;

            static string AsString(Neo.VM.Types.StackItem item) => item switch
            {
                Neo.VM.Types.Boolean _ => $"{item.GetBoolean()}",
                Neo.VM.Types.Buffer buffer => Neo.Helper.ToHexString(buffer.GetSpan()),
                Neo.VM.Types.ByteString byteString => Neo.Helper.ToHexString(byteString.GetSpan()),
                Neo.VM.Types.Integer @int => $"{@int.GetInteger()}",
                // Neo.VM.Types.InteropInterface _ => MakeVariable("InteropInterface"),
                // Neo.VM.Types.Map _ => MakeVariable("Map"),
                Neo.VM.Types.Null _ => "<null>",
                // Neo.VM.Types.Pointer _ => MakeVariable("Pointer"),
                // Neo.VM.Types.Array array => NeoArrayContainer.Create(manager, array, name),
                _ => throw new ArgumentException(nameof(item)),
            };

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
                        var (gasConsumed, results) = await blockchainOperations.TestInvokeContract(chain, InvocationFile);
                        console.WriteLine($"Gas Consumed: {gasConsumed}");
                        console.WriteLine("Result Stack:");
                        for (int i = 0; i < results.Length; i++)
                        {
                            console.WriteLine($"\t{AsString(results[i])}");
                        }
                    }
                    else
                    {
                        var account = blockchainOperations.GetAccount(chain, Account);
                        if (account == null)
                        {
                            throw new Exception($"{Account} account not found.");
                        }
                        var txHash = await blockchainOperations.InvokeContract(chain, InvocationFile, account, AdditionalGas);
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
