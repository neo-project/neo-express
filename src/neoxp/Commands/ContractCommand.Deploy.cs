using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;

namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
        [Command("deploy", Description = "Deploy contract to a neo-express instance")]
        class Deploy
        {
            readonly IBlockchainOperations blockchainOperations;

            public Deploy(IBlockchainOperations blockchainOperations)
            {
                this.blockchainOperations = blockchainOperations;
            }

            [Argument(0, Description = "Path to contract .nef file")]
            [Required]
            string Contract { get; } = string.Empty;

            [Argument(1, Description = "Account to pay contract deployment GAS fee")]
            [Required]
            string Account { get; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            [Option(Description = "Output as JSON")]
            bool Json { get; } = false;

            internal async Task<UInt256> ExecuteAsync()
            {
                var (chain, _) = blockchainOperations.LoadChain(Input);
                var account = chain.GetAccount(Account) ?? throw new Exception($"{Account} account not found.");
                var (nefFile, manifest) = await blockchainOperations.LoadContractAsync(Contract).ConfigureAwait(false);
                var expressNode = blockchainOperations.GetExpressNode(chain);
                return await expressNode.DeployAsync(nefFile, manifest, account);
            }

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var txHash = await ExecuteAsync().ConfigureAwait(false);
                    if (Json)
                    {
                        await console.Out.WriteLineAsync($"{txHash}");
                    }
                    else
                    {
                        await console.Out.WriteLineAsync($"Deployment Transaction {txHash} submitted");
                    }
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
