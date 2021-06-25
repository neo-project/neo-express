using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo.Network.P2P.Payloads;

namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
        [Command(Name = "run", Description = "Invoke a contract using parameters passed on command line")]
        internal class Run
        {
            readonly IExpressChainManagerFactory chainManagerFactory;
            readonly ITransactionExecutorFactory txExecutorFactory;

            public Run(IExpressChainManagerFactory chainManagerFactory, ITransactionExecutorFactory txExecutorFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
                this.txExecutorFactory = txExecutorFactory;
            }

            [Argument(0, Description = "Contract name or invocation hash")]
            [Required]
            internal string Contract { get; init; } = string.Empty;

            [Argument(1, Description = "Contract method to invoke")]
            [Required]
            internal string Method { get; init; } = string.Empty;

            [Argument(2, Description = "Contract method to invoke")]
            internal string[] Arguments { get; init; } = Array.Empty<string>();

            [Option(Description = "Account to pay contract invocation GAS fee")]
            internal string Account { get; init; } = string.Empty;

            [Option(Description = "Witness Scope to use for transaction signer (Default: CalledByEntry)")]
            [AllowedValues(StringComparison.OrdinalIgnoreCase, "None", "CalledByEntry", "Global")]
            internal WitnessScope WitnessScope { get; init; } = WitnessScope.CalledByEntry;

            [Option(Description = "Invoke contract for results (does not cost GAS)")]
            internal bool Results { get; init; } = false;

            [Option("--gas|-g", CommandOptionType.SingleValue, Description = "Additional GAS to apply to the contract invocation")]
            internal decimal AdditionalGas { get; init; } = 0;

            [Option(Description = "password to use for NEP-2/NEP-6 account")]
            internal string Password { get; init; } = string.Empty;

            [Option(Description = "Enable contract execution tracing")]
            internal bool Trace { get; init; } = false;

            [Option(Description = "Output as JSON")]
            internal bool Json { get; init; } = false;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            internal async Task<int> OnExecuteAsync(IConsole console)
            {
                try
                {
                    if (string.IsNullOrEmpty(Account) && !Results)
                    {
                        throw new Exception("Either --account or --results must be specified");
                    }

                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    using var txExec = txExecutorFactory.Create(chainManager, Trace, Json);
                    var script = await txExec.BuildInvocationScriptAsync(Contract, Method, Arguments).ConfigureAwait(false);

                    if (Results)
                    {
                        await txExec.InvokeForResultsAsync(script, Account, WitnessScope);
                    }
                    else
                    {
                        var password = chainManager.Chain.ResolvePassword(Account, Password);
                        await txExec.ContractInvokeAsync(script, Account, password, WitnessScope);
                    }


                    return 0;
                }
                catch (Exception ex)
                {
                    await console.Error.WriteLineAsync($"{ex.Message} [{ex.GetType().Name}]").ConfigureAwait(false);
                    while (ex.InnerException != null)
                    {
                        await console.Error.WriteLineAsync($"  Contract Exception: {ex.InnerException.Message} [{ex.InnerException.GetType().Name}]").ConfigureAwait(false);
                        ex = ex.InnerException;
                    }
                    return 1;
                }
            }
        }
    }
}
