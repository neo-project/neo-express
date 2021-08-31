using McMaster.Extensions.CommandLineUtils;
using System.Threading.Tasks;
using System;
using System.IO;

namespace NeoExpress.Commands
{
    partial class OracleCommand
    {
        [Command("requests", Description = "List outstanding oracle requests")]
        internal class Requests
        {
            readonly IExpressChainManagerFactory chainManagerFactory;

            public Requests(IExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            internal async Task ExecuteAsync(TextWriter writer)
            {
                var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                using var expressNode = chainManager.GetExpressNode();
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
