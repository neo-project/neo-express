using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    partial class OracleCommand
    {
        [Command("response", Description = "Submit oracle response")]
        internal class Response
        {
            readonly ExpressChainManagerFactory chainManagerFactory;
            readonly TransactionExecutorFactory txExecutorFactory;

            public Response(ExpressChainManagerFactory chainManagerFactory, TransactionExecutorFactory txExecutorFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
                this.txExecutorFactory = txExecutorFactory;
            }

            [Argument(0, Description = "URL of oracle request")]
            [Required]
            internal string Url { get; init; } = string.Empty;

            [Argument(1, Description = "Path to JSON file with oracle response content")]
            [Required]
            internal string ResponsePath { get; init; } = string.Empty;

            [Option(Description = "Oracle request ID")]
            internal ulong? RequestId { get; }

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            [Option(Description = "Enable contract execution tracing")]
            internal bool Trace { get; init; } = false;

            [Option(Description = "Output as JSON")]
            internal bool Json { get; init; } = false;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    using var txExec = txExecutorFactory.Create(chainManager, Trace, Json);
                    await txExec.OracleResponseAsync(Url, ResponsePath, RequestId).ConfigureAwait(false);
                    return 0;
                }
                catch (Exception ex)
                {
                    app.WriteException(ex, showInnerExceptions: true);
                    return 1;
                }
            }
        }
    }
}
