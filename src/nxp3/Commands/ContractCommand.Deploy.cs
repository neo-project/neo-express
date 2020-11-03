using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Neo3;

namespace nxp3.Commands
{
    partial class ContractCommand
    {
        [Command("deploy")]
        class Deploy
        {
            [Argument(0)]
            [Required]
            string Contract { get; } = string.Empty;

            [Argument(1)]
            [Required]
            string Account { get; } = string.Empty;

            [Option]
            string Input { get; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);

                    var blockchainOperations = new BlockchainOperations();
                    var account = blockchainOperations.GetAccount(chain, Account);
                    if (account == null)
                    {
                        throw new Exception($"Account {Account} not found.");
                    }

                    var txHash = await blockchainOperations.DeployContract(chain, Contract, account);
                    console.WriteLine($"InvocationTransaction {txHash} submitted");

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
