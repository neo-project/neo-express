using McMaster.Extensions.CommandLineUtils;
using System.Threading.Tasks;
using System;
using System.IO;
using System.IO.Abstractions;

namespace NeoExpress.Commands
{
    partial class OracleCommand
    {
        [Command("requests", Description = "List outstanding oracle requests")]
        internal class Requests
        {
            readonly IFileSystem fileSystem;

            public Requests(IFileSystem fileSystem)
            {
                this.fileSystem = fileSystem;
            }

            
            internal string Input { get; init; } = string.Empty;

            internal async Task ExecuteAsync(TextWriter writer)
            {
                var (chain, _) = fileSystem.LoadExpressChain(Input);
                using var expressNode = chain.GetExpressNode(fileSystem);
                var requests = await expressNode.ListOracleRequestsAsync().ConfigureAwait(false);

                foreach (var (id, request) in requests)
                {
                    await writer.WriteLineAsync($"request #{id}:").ConfigureAwait(false);
                    await writer.WriteLineAsync($"    Original Tx Hash: {request.OriginalTxid}").ConfigureAwait(false);
                    await writer.WriteLineAsync($"    Request Url:      \"{request.Url}\"").ConfigureAwait(false);
                }
            }

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    await ExecuteAsync(console.Out);
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
