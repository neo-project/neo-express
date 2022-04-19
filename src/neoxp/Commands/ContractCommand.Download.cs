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
            readonly ExpressChainManagerFactory chainManagerFactory;
            
            public Download(ExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }
            
            [Argument(0, Description = "Contract invocation hash")]
            [Required]
            internal string Contract { get; init; } = string.Empty;
            
            [Argument(1, Description = "URL of Neo JSON-RPC Node\nSpecify MainNet (default), TestNet or JSON-RPC URL")]
            internal string RpcUri { get; } = string.Empty;
            
            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;
            
            [Option(Description = "Block height to get contract state for")]
            internal uint Height { get; } = 0;

            [Option(CommandOptionType.SingleOrNoValue,
                Description = "Replace contract and storage if it already exists (Default: All)")]
            [AllowedValues(StringComparison.OrdinalIgnoreCase, "All", "ContractOnly", "StorageOnly")]
            internal OverwriteForce Force { get; init; } = OverwriteForce.None;

            internal async Task ExecuteAsync(TextWriter writer)
            {
                var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                
                if (chainManager.Chain.ConsensusNodes.Count != 1)
                {
                    throw new ArgumentException("Contract download is only supported for single-node consensus");
                }

                var result = await NodeUtility.DownloadContractStateAsync(Contract, RpcUri, Height);
                using var expressNode = chainManager.GetExpressNode();
                await expressNode.PersistContractAsync(result.contractState, result.storagePairs, Force);
            }
            
            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    await ExecuteAsync(console.Out).ConfigureAwait(false);
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