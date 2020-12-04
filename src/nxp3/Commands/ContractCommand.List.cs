using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Neo3;
using System;
using System.Linq;

namespace nxp3.Commands
{
    partial class ContractCommand
    {
        [Command(Name = "list")]
        private class List
        {
            [Option]
            string Input { get; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);

                    var blockchainOperations = new BlockchainOperations();
                    var contracts = await blockchainOperations.ListContracts(chain)
                        .ConfigureAwait(false);

                    foreach (var (hash, manifest) in contracts)
                    {
                        console.WriteLine($"{manifest.Name} ({hash})");
                    }

                    return 0;
                }
                catch (Exception ex)
                {
                    console.Error.WriteLine(ex.Message);
                    return 1;
                }
            }

        }
    }
}
