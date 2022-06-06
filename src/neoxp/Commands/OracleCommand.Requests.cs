using McMaster.Extensions.CommandLineUtils;
using System.Threading.Tasks;
using System;
using System.IO;
using System.IO.Abstractions;
using Newtonsoft.Json;

namespace NeoExpress.Commands
{
    partial class OracleCommand
    {
        [Command("requests", Description = "List outstanding oracle requests")]
        internal class Requests
        {
            readonly IExpressChain chain;

            public Requests(IExpressChain chain)
            {
                this.chain = chain;
            }

            public Requests(CommandLineApplication app) : this(app.GetExpressFile())
            {
            }

            [Option(Description = "Output as JSON")]
            internal bool Json { get; }

            internal Task<int> OnExecuteAsync(CommandLineApplication app)
                => app.ExecuteAsync(this.ExecuteAsync);

            internal async Task ExecuteAsync(IConsole console)
            {
                using var expressNode = chain.GetExpressNode();
                var requests = await expressNode.ListOracleRequestsAsync().ConfigureAwait(false);
                if (Json)
                {
                    using var writer = new JsonTextWriter(console.Out) { Formatting = Formatting.Indented };
                    using var _ = writer.WriteArray();
                    for (int i = 0; i < requests.Count; i++)
                    {
                        var (requestId, request) = requests[i];
                        using var __ = writer.WriteObject();
                        writer.WriteProperty("request-id", $"{requestId}");
                        writer.WriteProperty("original-tx-hash", $"{request.OriginalTxid}");
                        writer.WriteProperty("request-url", $"{request.Url}");
                    }
                }
                else
                {
                    for (int i = 0; i < requests.Count; i++)
                    {
                        var (requestId, request) = requests[i];
                        console.WriteLine($"request #{requestId}:");
                        console.WriteLine($"    Original Tx Hash: {request.OriginalTxid}");
                        console.WriteLine($"    Request Url:      \"{request.Url}\"");
                    }
                }
            }
        }
    }
}
