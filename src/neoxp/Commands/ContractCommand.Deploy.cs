using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;

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
            string Contract { get; } = string.Empty;

            [Argument(1, Description = "Account to pay contract deployment GAS fee")]
            [Required]
            string Account { get; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            [Option(Description = "Output as JSON")]
            bool Json { get; } = false;

            internal static async Task ExecuteAsync(IExpressChainManager chainManager, IExpressNode expressNode, IFileSystem fileSystem, string contract, string account, System.IO.TextWriter writer, bool json = false)
            {
                var (nefFile, manifest) = await fileSystem.LoadContractAsync(contract).ConfigureAwait(false);
                var _account = chainManager.Chain.GetAccount(account) ?? throw new Exception($"{account} account not found.");
                var txHash = await expressNode.DeployAsync(nefFile, manifest, _account).ConfigureAwait(false);
                await writer.WriteTxHashAsync(txHash, "Deployment", json).ConfigureAwait(false);
            }

            internal async Task<int> OnExecuteAsync(IConsole console)
            {
                try
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    using var expressNode = chainManager.GetExpressNode();
                    await ExecuteAsync(chainManager, expressNode, fileSystem, Contract, Account, console.Out, Json).ConfigureAwait(false);
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
