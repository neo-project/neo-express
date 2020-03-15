using McMaster.Extensions.CommandLineUtils;
using Neo;
using NeoExpress.Neo2;
using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;
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
                static string GetScriptHash(string Contract)
                {
                    if (UInt160.TryParse(Contract, out var _))
                    {
                        return Contract;
                    }

                    var blockchainOperations = new BlockchainOperations();
                    if (blockchainOperations.TryLoadContract(Contract, out var contract, out var errorMessage))
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
                    var result = await blockchainOperations.GetContract(chain, hash);
                    console.WriteResult(result);
                    return 0;
                }
                catch (Exception ex)
                {
                    console.WriteError(ex.Message);
                    app.ShowHelp();
                    return 1;
                }


            }

            // async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            // {
            //     try
            //     {
            //         var (chain, filename) = Program.LoadExpressChain(Input);
            //         var contract = chain.GetContract(Contract);
            //         if (contract == null)
            //         {
            //             throw new Exception($"Contract {Contract} not found.");
            //         }

            //         var uri = chain.GetUri();
            //         var result = await NeoRpcClient.GetContractState(uri, contract.Hash).ConfigureAwait(false);
            //         console.WriteResult(result);

            //         chain.SaveContract(contract, filename, console, Overwrite);
            //         return 0;
            //     }
            //     catch (Exception ex)
            //     {
            //         console.WriteError(ex.Message);
            //         app.ShowHelp();
            //         return 1;
            //     }
            // }
        }
    }
}
