using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Node;

namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
        internal enum OverwriteForce
        {
            None,
            All,
            ContractOnly,
            StorageOnly
        }

        [Command(Name = "download", Description = "Download contract with storage from remote chain into local chain")]
        internal class Download
        {
            readonly IExpressChain chain;

            public Download(IExpressChain chain)
            {
                this.chain = chain;
            }

            public Download(CommandLineApplication app)
            {
                this.chain = app.GetExpressFile();
            }

            [Argument(0, Description = "Contract invocation hash")]
            [Required]
            internal string Contract { get; init; } = string.Empty;

            [Argument(1, Description = "URL of Neo JSON-RPC Node\nSpecify MainNet (default), TestNet or JSON-RPC URL")]
            internal string RpcUri { get; } = string.Empty;

            [Option(Description = "Block height to get contract state for")]
            internal uint Height { get; } = 0;

            [Option(CommandOptionType.SingleOrNoValue,
                Description = "Replace contract and storage if it already exists (Default: All)")]
            [AllowedValues(StringComparison.OrdinalIgnoreCase, "All", "ContractOnly", "StorageOnly")]
            internal OverwriteForce Force { get; init; } = OverwriteForce.None;

            // internal static async Task ExecuteAsync(IExpressNode expressNode, string contract, string rpcUri, uint height, OverwriteForce force, TextWriter writer)
            // {
            //     var (state, storage) = await NodeUtility.DownloadContractStateAsync(contract, rpcUri, height)
            //         .ConfigureAwait(false);
            //     var storageCount = storage.Count == 1 ? "1 storage record" : $"{storage.Count} storage records";
            //     await expressNode.PersistContractAsync(state, storage, force).ConfigureAwait(false);
            //     await writer.WriteLineAsync($"{state.Manifest.Name} contract state and {storageCount} from {rpcUri} persisted successfully");
            // }

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    // var (chainManager, _) = chainManagerFactory.LoadChain(Input);

                    // if (chainManager.Chain.ConsensusNodes.Count != 1)
                    // {
                    //     throw new ArgumentException("Contract download is only supported for single-node consensus");
                    // }

                    // using var expressNode = chainManager.GetExpressNode();
                    // await ExecuteAsync(expressNode, Contract, RpcUri, Height, Force, console.Out).ConfigureAwait(false);
                    return 0;
                }
                catch (Exception ex)
                {
                    app.WriteException(ex);
                    return 1;
                }
            }
        }
    }
}