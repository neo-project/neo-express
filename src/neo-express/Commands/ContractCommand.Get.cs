using McMaster.Extensions.CommandLineUtils;
using Neo;
using NeoExpress.Neo2;
using NeoExpress.Neo2.Models;
using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace NeoExpress.Commands
{
    internal partial class ContractCommand
    {
        [Command(Name = "get")]
        private class Get
        {
            [Argument(0)]
            [Required]
            string Contract { get; } = string.Empty;

            [Option]
            private string Input { get; } = string.Empty;

            [Option]
            private bool Overwrite { get; }

            async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                static string GetScriptHash(string contract)
                {
                    if (UInt160.TryParse(contract, out var _))
                    {
                        return contract;
                    }

                    var blockchainOperations = new BlockchainOperations();
                    if (blockchainOperations.TryLoadContract(contract, out var contract, out var errorMessage))
                    {
                        return contract.Hash;
                    }

                    throw new Exception(errorMessage);
                }

                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);
                    var hash = GetScriptHash(Contract);
                    var blockchainOperations = new BlockchainOperations();
                    var contract = await blockchainOperations.GetContract(chain, hash);
                    if (contract != null)
                    {
                        var json = JsonConvert.SerializeObject(contract, Formatting.Indented);
                        console.WriteLine(json);
                    }
                    else
                    {
                        console.WriteError($"Contract {Contract} not found");
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
