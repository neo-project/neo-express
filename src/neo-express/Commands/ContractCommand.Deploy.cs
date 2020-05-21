using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NeoExpress.Commands
{

    internal partial class ContractCommand
    {
        [Command(Name = "deploy")]
        private class Deploy
        {
            [Argument(0)]
            [Required]
            string Contract { get; } = string.Empty;

            [Argument(1)]
            [Required]
            string Account { get; } = string.Empty;

            [Option]
            private string Input { get; } = string.Empty;

            [Option(CommandOptionType.SingleValue)]
            private bool SaveMetadata { get; } = true;

            async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);

                    var account = chain.GetAccount(Account);
                    if (account == null)
                    {
                        throw new Exception($"Account {Account} not found.");
                    }

                    if (BlockchainOperations.TryLoadContract(Contract, out var contract, out var errorMessage))
                    {
                        console.WriteLine($"Deploying contract {contract.Name} ({contract.Hash}) {(SaveMetadata ? "and contract metadata" : "")}");
                        var tx = await BlockchainOperations.DeployContract(chain, contract, account, SaveMetadata);
                        console.WriteLine($"InvocationTransaction {tx.Hash} submitted");
                    }
                    else
                    {
                        throw new Exception(errorMessage);
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
