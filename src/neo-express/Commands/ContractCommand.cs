using System;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using NeoExpress.Neo2;

namespace NeoExpress.Commands
{
    [Command(Name = "contract")]
    [Subcommand(
        typeof(Deploy),
        typeof(Get),
        // typeof(Invoke),
        typeof(List),
        typeof(Storage))]
    internal partial class ContractCommand
    {
        static string GetScriptHash(string contract)
        {
            if (UInt160.TryParse(contract, out var _))
            {
                return contract;
            }

            var blockchainOperations = new BlockchainOperations();
            if (blockchainOperations.TryLoadContract(contract, out var _contract, out var errorMessage))
            {
                return _contract.Hash;
            }

            throw new Exception(errorMessage);
        }

        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteError("You must specify at a subcommand.");
            app.ShowHelp();
            return 1;
        }
    }
}
