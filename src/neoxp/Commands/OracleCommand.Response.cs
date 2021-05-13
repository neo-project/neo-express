using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo.Network.P2P.Payloads;
using Newtonsoft.Json.Linq;

namespace NeoExpress.Commands
{
    partial class OracleCommand
    {
        [Command("response", Description = "Submit oracle response")]
        internal class Response
        {
            readonly IExpressChainManagerFactory chainManagerFactory;
            readonly ITransactionExecutorFactory txExecutorFactory;

            public Response(IExpressChainManagerFactory chainManagerFactory, ITransactionExecutorFactory txExecutorFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
                this.txExecutorFactory = txExecutorFactory;
            }

            [Argument(0, Description = "URL of oracle request")]
            [Required]
            internal string Url { get; init; } = string.Empty;

            [Argument(1, Description = "Path to JSON file with oracle response cotnent")]
            [Required]
            internal string ResponsePath { get; init; } = string.Empty;

            [Option(Description = "Oracle request ID")]
            internal (bool hasValue, ulong value) RequestId { get; }

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            [Option(Description = "Output as JSON")]
            internal bool Json { get; init; } = false;

            // internal static async Task ExecuteAsync(IExpressChainManager chainManager, IExpressNode expressNode, IFileSystem fileSystem, string url, string responsePath, ulong? requestId, System.IO.TextWriter writer, bool json = false)
            // {
            //     if (!fileSystem.File.Exists(responsePath)) throw new Exception($"Response File {responsePath} couldn't be found");

            //     JObject responseJson;
            //     {
            //         using var stream = fileSystem.File.OpenRead(responsePath);
            //         using var reader = new System.IO.StreamReader(stream);
            //         using var jsonReader = new Newtonsoft.Json.JsonTextReader(reader);
            //         responseJson = await JObject.LoadAsync(jsonReader).ConfigureAwait(false);
            //     }

            //     var txHashes = await expressNode.SubmitOracleResponseAsync(url, OracleResponseCode.Success, responseJson, requestId).ConfigureAwait(false);

            //     if (json)
            //     {
            //         using var jsonWriter = new Newtonsoft.Json.JsonTextWriter(writer);
            //         await jsonWriter.WriteStartArrayAsync().ConfigureAwait(false);
            //         for (int i = 0; i < txHashes.Count; i++)
            //         {
            //             await jsonWriter.WriteValueAsync(txHashes[i].ToString()).ConfigureAwait(false);
            //         }
            //         await jsonWriter.WriteEndArrayAsync().ConfigureAwait(false);
            //     }
            //     else
            //     {
            //         if (txHashes.Count == 0)
            //         {
            //             await writer.WriteLineAsync("No oracle response transactions submitted").ConfigureAwait(false);
            //         }
            //         else
            //         {
            //             await writer.WriteLineAsync("Oracle response transactions submitted:").ConfigureAwait(false);
            //             for (int i = 0; i < txHashes.Count; i++)
            //             {
            //                 await writer.WriteLineAsync($"    {txHashes[i]}").ConfigureAwait(false);
            //             }
            //         }
            //     }
            // }

            internal async Task<int> OnExecuteAsync(IConsole console)
            {
                try
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    using var txExec = txExecutorFactory.Create(chainManager, false, Json);
                    await txExec.OracleResponseAsync(Url, ResponsePath, RequestId.hasValue ? RequestId.value : null).ConfigureAwait(false);
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
