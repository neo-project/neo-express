using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Neo3;
using System;

namespace nxp3.Commands
{
    partial class ContractCommand
    {
        [Command(Name = "list")]
        private class List
        {
            [Option]
            private string Input { get; } = string.Empty;

            async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);

                    var blockchainOperations = new BlockchainOperations();
                    var contracts = await blockchainOperations.ListContracts(chain)
                        .ConfigureAwait(false);

                    for (int i = 0; i < contracts.Count; i++)
                    {
                        console.WriteLine(contracts[i].ToJson().ToString(true));
                    }

                    return 0;
                }
                catch (Exception ex)
                {
                    console.WriteLine(ex.Message);
                    return 1;
                }
            }

        }
    }
}
