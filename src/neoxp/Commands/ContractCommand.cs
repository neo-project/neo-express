using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.SmartContract.Manifest;

namespace NeoExpress.Commands
{
    [Command("contract", Description = "Manage smart contracts")]
    [Subcommand(typeof(Deploy), typeof(Download), typeof(Get), typeof(Hash), typeof(Invoke), typeof(List), typeof(Run), typeof(Storage))]
    partial class ContractCommand
    {
        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp(false);
            return 1;
        }

        
        public static async Task<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>> ListByNameAsync(IExpressNode expressNode, string name)
        {
            var contracts = await expressNode.ListContractsAsync().ConfigureAwait(false);
            return contracts
                .Where(t => string.Equals(name, t.manifest.Name, StringComparison.OrdinalIgnoreCase))
                .ToReadOnlyList();
        }
    }
}
