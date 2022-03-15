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
            private RpcClient rpcClient = null!;
            
            public Download(ExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }
            
            [Argument(0, Description = "Contract invocation hash")]
            [Required]
            internal string Contract { get; init; } = string.Empty;
            
            [Argument(1, Description = "Source network RPC address")]
            internal string Source { get; init; } = string.Empty;
            
            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            internal async Task ExecuteAsync(TextWriter writer)
            {
                var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                var expressNode = chainManager.GetExpressNode();

                this.rpcClient = new RpcClient(new Uri(Source));
                
                if (!UInt160.TryParse(Contract, out _))
                {
                    await writer.WriteLineAsync($"Invalid contract hash: {Contract} ").ConfigureAwait(false);
                }
                else
                {
                    // 1. Get ContractState
                    var state = await this.rpcClient.GetContractStateAsync(Contract).ConfigureAwait(false);
                    
                    // 2. Get Full storage of the contract
                    var storage_pairs = (JArray)await this.rpcClient.RpcSendAsync("getfullstorage", Contract).ConfigureAwait(false);
                    
                    await writer.WriteLineAsync(storage_pairs.ToString(true)).ConfigureAwait(false);
                    await expressNode.PersistContractAsync(state, storage_pairs);
                }
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