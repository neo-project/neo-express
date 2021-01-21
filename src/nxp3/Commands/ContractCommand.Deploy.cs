using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;


namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
        [Command("deploy", Description = "Deploy contract to a neo-express instance")]
        class Deploy
        {
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
                    if (Json)
                    {
                        console.WriteLine($"{txHash}");
                    } 
                    else
                    {
                        console.WriteLine($"Deployment Transaction {txHash} submitted");
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
