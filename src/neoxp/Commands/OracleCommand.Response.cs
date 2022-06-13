using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.Network.P2P.Payloads;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeoExpress.Commands
{
    partial class OracleCommand
    {
        [Command("response", Description = "Submit oracle response")]
        internal class Response
        {
            readonly IExpressChain chain;

            public Response(IExpressChain chain)
            {
                this.chain = chain;
            }

            public Response(CommandLineApplication app)
            {
                this.chain = app.GetExpressFile();
            }

            [Argument(0, Description = "URL of oracle request")]
            [Required]
            internal string Url { get; init; } = string.Empty;

            [Argument(1, Description = "Path to JSON file with oracle response content")]
            [Required]
            internal string ResponsePath { get; init; } = string.Empty;

            [Option(Description = "Oracle request ID")]
            internal ulong? RequestId { get; }

            [Option(Description = "Enable contract execution tracing")]
            internal bool Trace { get; init; } = false;

            [Option(Description = "Output as JSON")]
            internal bool Json { get; init; } = false;

            internal Task<int> OnExecuteAsync(CommandLineApplication app)
                => app.ExecuteAsync(this.ExecuteAsync);

            internal async Task ExecuteAsync(IFileSystem fileSystem, IConsole console)
            {
                if (!fileSystem.File.Exists(ResponsePath)) throw new Exception($"Response File {ResponsePath} couldn't be found");

                var responseJson = await LoadResponseAsync(fileSystem, ResponsePath).ConfigureAwait(false);

                using var expressNode = chain.GetExpressNode();
                var txHashes = await ExecuteAsync(expressNode, Url, OracleResponseCode.Success, responseJson, RequestId).ConfigureAwait(false);

                if (Json)
                {
                    using var writer = new JsonTextWriter(console.Out) { Formatting = Formatting.Indented };
                    using var _ = writer.WriteArray();
                    for (int i = 0; i < txHashes.Count; i++)
                    {
                        writer.WriteValue($"{txHashes[i]}");
                    }
                }
                else
                {
                    if (txHashes.Count == 0)
                    {
                        console.WriteLine("No oracle response transactions submitted");
                    }
                    else
                    {
                        console.WriteLine($"{txHashes.Count} Oracle response transactions submitted:");
                        for (int i = 0; i < txHashes.Count; i++)
                        {
                            console.WriteLine($"    {txHashes[i]}");
                        }
                    }
                }
            }

            public static async Task<JObject> LoadResponseAsync(IFileSystem fileSystem, string responsePath)
            {
                using var stream = fileSystem.File.OpenRead(responsePath);
                using var reader = new System.IO.StreamReader(stream);
                using var jsonReader = new JsonTextReader(reader);
                return await JObject.LoadAsync(jsonReader).ConfigureAwait(false);
            }

            public static async Task<IReadOnlyList<UInt256>> ExecuteAsync(IExpressNode expressNode, string url, OracleResponseCode responseCode, Newtonsoft.Json.Linq.JObject? responseJson, ulong? requestId)
            {
                if (responseCode == OracleResponseCode.Success && responseJson == null)
                {
                    throw new ArgumentException("responseJson cannot be null when responseCode is Success", nameof(responseJson));
                }

                var requests = await expressNode.ListOracleRequestsAsync().ConfigureAwait(false);

                var txHashes = new List<UInt256>();
                for (var x = 0; x < requests.Count; x++)
                {
                    var (id, request) = requests[x];
                    if (requestId.HasValue && requestId.Value != id) continue;
                    if (!string.Equals(url, request.Url, StringComparison.OrdinalIgnoreCase)) continue;

                    var response = new OracleResponse
                    {
                        Code = responseCode,
                        Id = id,
                        Result = GetResponseData(request.Filter),
                    };

                    var txHash = await expressNode.SubmitOracleResponseAsync(response);
                    txHashes.Add(txHash);
                }
                return txHashes;

                byte[] GetResponseData(string filter)
                {
                    if (responseCode != OracleResponseCode.Success)
                    {
                        return Array.Empty<byte>();
                    }

                    System.Diagnostics.Debug.Assert(responseJson != null);

                    var json = string.IsNullOrEmpty(filter)
                        ? (JContainer)responseJson
                        : new JArray(responseJson.SelectTokens(filter, true));
                    return Neo.Utility.StrictUTF8.GetBytes(json.ToString());
                }
            }
        }
    }
}
