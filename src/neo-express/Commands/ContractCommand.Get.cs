using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

            async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);
                    var hash = GetScriptHash(Contract);

                    var contract = await BlockchainOperations.GetContract(chain, hash);
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
