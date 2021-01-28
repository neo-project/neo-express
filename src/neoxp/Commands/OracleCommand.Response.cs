using System;
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
        class Response
        {
            readonly IExpressChainManagerFactory chainManagerFactory;
            readonly IFileSystem fileSystem;

            public Response(IExpressChainManagerFactory chainManagerFactory, IFileSystem fileSystem)
            {
                this.chainManagerFactory = chainManagerFactory;
                this.fileSystem = fileSystem;
            }

            [Argument(0, Description = "URL of oracle request")]
            string Url { get; } = string.Empty;

            [Argument(1, Description = "Path to JSON file with oracle response cotnent")]
            string ResponsePath { get; } = string.Empty;

            [Option(Description = "Oracle request ID")]
            (bool hasValue, ulong value) RequestId { get; }

            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            [Option(Description = "Output as JSON")]
            bool Json { get; } = false;

            internal async Task ExecuteAsync(System.IO.TextWriter writer)
            {
                var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                if (!fileSystem.File.Exists(ResponsePath)) throw new Exception($"Response File {ResponsePath} couldn't be found");

                JObject responseJson;
                {
                    using var stream = fileSystem.File.OpenRead(ResponsePath);
                    using var reader = new System.IO.StreamReader(stream);
                    using var jsonReader = new Newtonsoft.Json.JsonTextReader(reader);
                    responseJson = await JObject.LoadAsync(jsonReader).ConfigureAwait(false);
                }

                var requestId = RequestId.hasValue ? (ulong?)RequestId.value : null;

                using var expressNode = chainManager.GetExpressNode();
                var txHashes = await expressNode.SubmitOracleResponseAsync(Url, OracleResponseCode.Success, responseJson, requestId).ConfigureAwait(false);

                if (Json)
                {
                    using var jsonWriter = new Newtonsoft.Json.JsonTextWriter(writer);
                    await jsonWriter.WriteStartArrayAsync().ConfigureAwait(false);
                    for (int i = 0; i < txHashes.Count; i++)
                    {
                        await jsonWriter.WriteValueAsync(txHashes[i].ToString()).ConfigureAwait(false);
                    }
                    await jsonWriter.WriteEndArrayAsync().ConfigureAwait(false);
                }
                else
                {
                    if (txHashes.Count == 0)
                    {
                        await writer.WriteLineAsync("No oracle response transactions submitted").ConfigureAwait(false);
                    }
                    else
                    {
                        await writer.WriteLineAsync("Oracle response transactions submitted:").ConfigureAwait(false);
                        for (int i = 0; i < txHashes.Count; i++)
                        {
                            await writer.WriteLineAsync($"    {txHashes[i]}").ConfigureAwait(false);
                        }
                    }
                }
            }

            internal async Task<int> OnExecuteAsync(IConsole console)
            {
                try
                {
                    await ExecuteAsync(console.Out).ConfigureAwait(false);
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
