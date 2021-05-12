using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo.Network.P2P.Payloads;

namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
        [Command("deploy", Description = "Deploy contract to a neo-express instance")]
        internal class Deploy
        {
            readonly IExpressChainManagerFactory chainManagerFactory;
            readonly IFileSystem fileSystem;

            public Deploy(IExpressChainManagerFactory chainManagerFactory, IFileSystem fileSystem)
            {
                this.chainManagerFactory = chainManagerFactory;
                this.fileSystem = fileSystem;
            }

            [Argument(0, Description = "Path to contract .nef file")]
            [Required]
            internal string Contract { get; init; } = string.Empty;

            [Argument(1, Description = "Account to pay contract deployment GAS fee")]
            [Required]
            internal string Account { get; init; } = string.Empty;

            [Option(Description = "Witness Scope to use for transaction signer (Default: CalledByEntry)")]
            [AllowedValues(StringComparison.OrdinalIgnoreCase, "None", "CalledByEntry", "Global")]
            internal WitnessScope WitnessScope { get; init; } = WitnessScope.CalledByEntry;

            [Option(Description = "password to use for NEP-2/NEP-6 account")]
            internal string Password { get; init; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            [Option(Description = "Enable contract execution tracing")]
            internal bool Trace { get; init; } = false;

            [Option(Description = "Deploy contract regardless of name conflict")]
            internal bool Force { get; }

            [Option(Description = "Output as JSON")]
            internal bool Json { get; init; } = false;

            internal static async Task ExecuteAsync(IExpressChainManager chainManager, IExpressNode expressNode, IFileSystem fileSystem, string contract, string accountName, string password, WitnessScope witnessScope, bool force, bool json, System.IO.TextWriter writer)
            {
                if (!chainManager.Chain.TryGetAccount(accountName, out var wallet, out var account, chainManager.ProtocolSettings))
                {
                    throw new Exception($"{accountName} account not found.");
                }

                var (nefFile, manifest) = await fileSystem.LoadContractAsync(contract).ConfigureAwait(false);

                if (!force)
                {
                    var contracts = await expressNode.ListContractsAsync(manifest.Name).ConfigureAwait(false);
                    if (contracts.Count > 0)
                    {
                        throw new Exception($"Contract named {manifest.Name} already deployed. Use --force to deploy contract with conflicting name.");
                    }
                }

                var txHash = await expressNode.DeployAsync(nefFile, manifest, wallet, account.ScriptHash, witnessScope).ConfigureAwait(false);
                await writer.WriteTxHashAsync(txHash, "Deployment", json).ConfigureAwait(false);
            }

            internal async Task<int> OnExecuteAsync(IConsole console)
            {
                try
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    using var expressNode = chainManager.GetExpressNode(Trace);
                    var password = chainManager.Chain.GetPassword(Account, Password);
                    await ExecuteAsync(chainManager, expressNode, fileSystem, Contract, Account, password, WitnessScope, Force, Json, console.Out).ConfigureAwait(false);
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
