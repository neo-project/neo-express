using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.IO.Json;
using Neo.Network.RPC;

namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
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

            internal async Task ExecuteAsync(TextWriter writer)
            {
                var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                var expressNode = chainManager.GetExpressNode();

                if (!UInt160.TryParse(Contract, out var contractHash))
                {
                    throw new ArgumentException($"Invalid contract hash: \"{Contract}\"");    
                }

                if (!TransactionExecutor.TryParseRpcUri(RpcUri, out var uri))
                {
                    throw new ArgumentException($"Invalid RpcUri value \"{RpcUri}\"");
                }

                using var rpcClient = new RpcClient(uri);
                var stateAPI = new StateAPI(rpcClient);

                var stateHeight = await stateAPI.GetStateHeightAsync();
                if (stateHeight.localRootIndex is null)
                {
                    throw new Exception("Null \"localRootIndex\" in state height response");
                }

                var stateRoot = await stateAPI.GetStateRootAsync(stateHeight.localRootIndex.Value);
                var states = await rpcClient.ExpressFindStatesAsync(stateRoot.RootHash, contractHash, new byte[0]);
                var contractState = await rpcClient.GetContractStateAsync(Contract).ConfigureAwait(false);

                await expressNode.PersistContractAsync(contractState, states.Results);
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