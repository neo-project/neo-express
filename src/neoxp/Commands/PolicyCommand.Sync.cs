using System.ComponentModel.DataAnnotations;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    partial class PolicyCommand
    {
        [Command(Name = "sync", Description = "Synchronize local policy values with public Neo network")]
        internal class Sync
        {
            readonly ExpressChainManagerFactory chainManagerFactory;
            readonly TransactionExecutorFactory txExecutorFactory;

            public Sync(ExpressChainManagerFactory chainManagerFactory, TransactionExecutorFactory txExecutorFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
                this.txExecutorFactory = txExecutorFactory;
            }

            [Argument(0, Description = "Source of policy values. Must be local policy settings JSON file or the URL of Neo JSON-RPC Node\nFor Node URL,\"MainNet\" or \"TestNet\" can be specified in addition to a standard HTTP URL")]
            [Required]
            internal string Source { get; } = string.Empty;

            [Argument(1, Description = "Account to pay contract invocation GAS fee")]
            [Required]
            internal string Account { get; init; } = string.Empty;

            [Option(Description = "password to use for NEP-2/NEP-6 sender")]
            internal string Password { get; init; } = string.Empty;

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

                    var values = await txExec.TryGetRemoteNetworkPolicyAsync(Source).ConfigureAwait(false);
                    if (values.IsT1)
                    {
                        values = await txExec.TryLoadPolicyFromFileSystemAsync(Source).ConfigureAwait(false);
                    }

                    if (values.TryPickT0(out var policyValues, out var _))
                    {
                        await txExec.SetPolicyAsync(policyValues, Account, Password).ConfigureAwait(false);
                    }
                    else
                    {
                        throw new ArgumentException($"Could not load policy values from \"{Source}\"");
                    }

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
