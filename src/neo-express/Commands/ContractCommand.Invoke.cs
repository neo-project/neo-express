using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Neo2;
using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
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

            [Option]
            private string Input { get; } = string.Empty;

            [Option]
            private string Account { get; } = string.Empty;

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
                    var account = blockchainOperations.GetAccount(chain, Account);
                    if (account == null)
                    {
                        throw new Exception("Invalid Account");
                    }
                    
                    var tx = await blockchainOperations.InvokeContract(chain, InvocationFile, account);
                    console.WriteLine($"InvocationTransaction {tx.Hash} submitted");
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
