using System;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.BlockchainToolkit;
using Neo.SmartContract.Native;

namespace NeoExpress.Commands
{
    [Command("policy", Description = "Manage blockchain policy")]
    [Subcommand(typeof(Block), typeof(Get), typeof(IsBlocked), typeof(Set), typeof(Sync), typeof(Unblock))]
    partial class PolicyCommand
    {
        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp(false);
            return 1;
        }

        public static async Task<UInt160> ResolveScriptHashAsync(IExpressNode expressNode, string name)
        {
            var chain = expressNode.Chain;

            if (chain.IsReservedName(name)) 
            {
                throw new Exception($"Can't block consensus account {name}");
            }

            if (chain.TryResolveAccountHash(name, out var accountHash))
            {
                return accountHash;
            }

            if (NativeContract.Contracts.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new Exception($"Can't block native contract {name}");
            }

            var result = await expressNode.TryGetContractHashAsync(name).ConfigureAwait(false);
            if (result.TryPickT0(out var contractHash, out _))
            {
                return contractHash;
            }

            throw new Exception($"{name} script hash not found");
        }
    }
}