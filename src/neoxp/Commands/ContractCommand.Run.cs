using System;
using System.Collections.Generic;
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
            readonly IExpressChain chain;

            public Run(IExpressChain chain)
            {
                this.chain = chain;
            }

            public Run(CommandLineApplication app)
            {
                this.chain = app.GetExpressFile();
            }

            [Argument(0, Description = "Contract name or invocation hash")]
            [Required]
            internal string Contract { get; init; } = string.Empty;

            [Argument(1, Description = "Contract method to invoke")]
            [Required]
            internal string Method { get; init; } = string.Empty;

            [Argument(2, Description = "Arguments to pass to the invoked method")]
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

            internal Task<int> OnExecuteAsync(CommandLineApplication app)
                => app.ExecuteAsync(this.ExecuteAsync);

            internal async Task ExecuteAsync(IConsole console)
            {
                using var expressNode = chain.GetExpressNode(Trace);
                var script = await expressNode.BuildInvocationScriptAsync(Contract, Method, Arguments).ConfigureAwait(false);

                if (Results)
                {
                    var result = await expressNode.InvokeForResultsAsync(script, Account, WitnessScope);
                    console.Out.WriteResult(result, Json);
                }
                else
                {
                    var password = chain.ResolvePassword(Account, Password);
                    var txHash = await expressNode.SubmitTransactionAsync(script, Account, password, WitnessScope, AdditionalGas);
                    await console.Out.WriteTxHashAsync(txHash, "Invocation", Json).ConfigureAwait(false);
                }
            }
        }
    }
}
