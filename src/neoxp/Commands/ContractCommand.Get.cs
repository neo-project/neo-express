using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

using System;

namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
        [Command(Name = "get", Description = "Get information for a deployed contract")]
        private class Get
        {
            readonly IBlockchainOperations blockchainOperations;

            public Get(IBlockchainOperations blockchainOperations)
            {
                this.blockchainOperations = blockchainOperations;
            }

            [Argument(0, Description = "Contract name or invocation hash")]
            [Required]
            string Contract { get; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = blockchainOperations.LoadChain(Input);

                    // var (chain, _) = Program.LoadExpressChain(Input);

                    // var blockchainOperations = new BlockchainOperations();
                    // var manifest = await blockchainOperations.GetContractAsync(chain, Contract)
                    //     .ConfigureAwait(false);

                    // console.WriteLine(manifest.ToJson().ToString(true));

                    return 0;
                }
                catch (Exception ex)
                {
                    await console.Error.WriteLineAsync(ex.Message);
                    return 1;
                }
            }
        }
    }
}
