using System;
using System.ComponentModel.DataAnnotations;
using System.Numerics;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Models;

namespace NeoExpress.Commands
{
    partial class PolicyCommand
    {
        [Command(Name = "set", Description = "Get information for a deployed contract")]
        internal class Set
        {
            readonly IExpressChainManagerFactory chainManagerFactory;
            readonly ITransactionExecutorFactory txExecutorFactory;

            public Set(IExpressChainManagerFactory chainManagerFactory, ITransactionExecutorFactory txExecutorFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
                this.txExecutorFactory = txExecutorFactory;
            }

            [Argument(0, Description = "Policy to set")]
            [Required]
            internal PolicyName Policy { get; init; }

            [Argument(1, Description = "New Policy Value")]
            [Required]
            internal long Value { get; set; }

            [Argument(2, Description = "Account to pay contract invocation GAS fee")]
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

            internal async Task<int> OnExecuteAsync(IConsole console)
            {
                try
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    using var txExec = txExecutorFactory.Create(chainManager, Trace, Json);
                    await txExec.SetPolicyAsync(Policy, Value, Account, Password).ConfigureAwait(false);
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
