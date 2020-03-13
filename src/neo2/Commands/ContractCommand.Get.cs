using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Neo2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace NeoExpress.Neo2.Commands
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
                try
                {
                    var (chain, filename) = Program.LoadExpressChain(Input);
                    var contract = chain.GetContract(Contract);
                    if (contract == null)
                    {
                        throw new Exception($"Contract {Contract} not found.");
                    }

                    var uri = chain.GetUri();
                    var result = await NeoRpcClient.GetContractState(uri, contract.Hash).ConfigureAwait(false);
                    console.WriteResult(result);

                    chain.SaveContract(contract, filename, console, Overwrite);
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
