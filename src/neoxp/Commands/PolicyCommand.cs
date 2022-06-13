using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using OneOf;

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

            var result = await TryGetContractHashAsync(expressNode, name).ConfigureAwait(false);
            if (result.TryPickT0(out var contractHash, out _))
            {
                return contractHash;
            }

            throw new Exception($"{name} script hash not found");

            static async Task<OneOf<UInt160,OneOf.Types.NotFound>> TryGetContractHashAsync(IExpressNode expressNode, string name)
            {
                var contracts = await expressNode.ListContractsAsync().ConfigureAwait(false);
                if (TryGetContractHash(contracts, name, StringComparison.OrdinalIgnoreCase, out var contractHash))
                {
                    return contractHash;
                }
                return default(OneOf.Types.NotFound);
            }

            static bool TryGetContractHash(IReadOnlyList<(UInt160 hash, ContractManifest manifest)> contracts, string name, StringComparison comparison, out UInt160 scriptHash)
            {
                UInt160? _scriptHash = null;
                for (int i = 0; i < contracts.Count; i++)
                {
                    if (contracts[i].manifest.Name.Equals(name, comparison))
                    {
                        if (_scriptHash == null)
                        {
                            _scriptHash = contracts[i].hash;
                        }
                        else
                        {
                            throw new Exception($"More than one deployed script named {name}");
                        }
                    }
                }

                scriptHash = _scriptHash!;
                return _scriptHash != null;
            }
        }
    }
}