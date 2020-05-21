using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NeoExpress.Commands
{
    internal partial class ContractCommand
    {
        [Command(Name = "list")]
        private class List
        {
            [Option]
            private string Input { get; } = string.Empty;

            private async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);
                    var contracts = await BlockchainOperations.ListContracts(chain);
                    for (int i = 0; i < contracts.Count; i++)
                    {
                        var contract = contracts[i];
                        var json = JsonConvert.SerializeObject(contract, Formatting.Indented);
                        console.WriteLine(json);
                    }

                    if (contracts.Count == 0)
                    {
                        console.WriteLine("no contracts deployed");
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
