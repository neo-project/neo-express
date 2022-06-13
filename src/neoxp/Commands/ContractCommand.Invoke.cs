using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using Neo.VM;
using Newtonsoft.Json;

namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
        [Command(Name = "invoke", Description = "Invoke a contract using parameters from .neo-invoke.json file")]
        internal class Invoke
        {
            readonly IExpressChain chain;

            public Invoke(IExpressChain chain)
            {
                this.chain = chain;
            }

            public Invoke(CommandLineApplication app)
            {
                this.chain = app.GetExpressFile();
            }

            [Argument(0, Description = "Path to contract invocation JSON file")]
            [Required]
            internal string InvocationFile { get; init; } = string.Empty;

            [Argument(1, Description = "Account to pay contract invocation GAS fee")]
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

            internal async Task ExecuteAsync(IFileSystem fileSystem, IConsole console)
            {
                using var expressNode = chain.GetExpressNode(Trace);

                if (!fileSystem.File.Exists(InvocationFile))
                {
                    throw new Exception($"Invocation file {InvocationFile} couldn't be found");
                }

                var parser = await expressNode.GetContractParameterParserAsync().ConfigureAwait(false);
                var script = await parser.LoadInvocationScriptAsync(InvocationFile).ConfigureAwait(false);

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
