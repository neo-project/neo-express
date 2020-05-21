using System;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    [Command(Name = "contract")]
    [Subcommand(
        typeof(Deploy),
        typeof(Get),
        typeof(Invoke),
        typeof(List),
        typeof(Storage))]
    internal partial class ContractCommand
    {

        static string GetScriptHash(string contract)
        {
            if (Neo.UInt160.TryParse(contract, out var _))
            {
                return contract;
            }

            if (BlockchainOperations.TryLoadContract(contract, out var _contract, out var errorMessage))
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
