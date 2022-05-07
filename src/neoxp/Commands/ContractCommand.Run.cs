using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Numerics;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.BlockchainToolkit;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.VM;

namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
        [Command(Name = "run", Description = "Invoke a contract using parameters passed on command line")]
        internal class Run
        {
            readonly IExpressFile expressFile;

            public Run(IExpressFile expressFile)
            {
                this.expressFile = expressFile;
            }

            public Run(CommandLineApplication app) : this(app.GetExpressFile())
            {
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
                using var expressNode = expressFile.GetExpressNode(Trace);
                var script = await BuildInvocationScriptAsync(expressNode, Contract, Method, Arguments).ConfigureAwait(false);

                if (Results)
                {
                    var signer = expressNode.Chain.TryResolveAccountHash(Account, out var hash)
                        ? new Signer { Account = hash, Scopes = WitnessScope }
                        : null;
                    var result = await expressNode.InvokeAsync(script, signer).ConfigureAwait(false);
                    await console.Out.WriteLineAsync(result.ToJson().ToString(true)).ConfigureAwait(false);
                }
                else
                {
                    var password = expressFile.ResolvePassword(Account, Password);
                    var (wallet, accountHash) = expressNode.ExpressFile.ResolveSigner(Account, password);

                    var txHash = await expressNode.ExecuteAsync(wallet, accountHash, WitnessScope, script).ConfigureAwait(false);
                    await console.Out.WriteTxHashAsync(txHash, "Invocation", Json).ConfigureAwait(false);
                }
            }

            public static async Task<Script> BuildInvocationScriptAsync(IExpressNode expressNode, string contract, string operation, IReadOnlyList<string>? arguments = null)
            {
                if (string.IsNullOrEmpty(operation))
                    throw new InvalidOperationException($"invalid contract operation \"{operation}\"");

                var parser = await expressNode.GetContractParameterParserAsync().ConfigureAwait(false);
                var scriptHash = parser.TryLoadScriptHash(contract, out var value)
                    ? value
                    : UInt160.TryParse(contract, out var uint160)
                        ? uint160
                        : throw new InvalidOperationException($"contract \"{contract}\" not found");

                arguments ??= Array.Empty<string>();
                var @params = new ContractParameter[arguments.Count];
                for (int i = 0; i < arguments.Count; i++)
                {
                    @params[i] = ConvertArg(arguments[i], parser);
                }

                using var scriptBuilder = new ScriptBuilder();
                scriptBuilder.EmitDynamicCall(scriptHash, operation, @params);
                return scriptBuilder.ToArray();

                static ContractParameter ConvertArg(string arg, ContractParameterParser parser)
                {
                    if (bool.TryParse(arg, out var boolArg))
                    {
                        return new ContractParameter()
                        {
                            Type = ContractParameterType.Boolean,
                            Value = boolArg
                        };
                    }

                    if (long.TryParse(arg, out var longArg))
                    {
                        return new ContractParameter()
                        {
                            Type = ContractParameterType.Integer,
                            Value = new BigInteger(longArg)
                        };
                    }

                    return parser.ParseParameter(arg);
                }
            }

            // internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            // {
            //     try
            //     {
            //         if (string.IsNullOrEmpty(Account) && !Results)
            //         {
            //             throw new Exception("Either --account or --results must be specified");
            //         }

            //         var (chain, _) = fileSystem.LoadExpressChain(Input);
            //         using var txExec = new TransactionExecutor(fileSystem, chain, Trace, Json, console.Out); 
            //         var script = await txExec.BuildInvocationScriptAsync(Contract, Method, Arguments).ConfigureAwait(false);

            //         if (Results)
            //         {
            //             await txExec.InvokeForResultsAsync(script, Account, WitnessScope);
            //         }
            //         else
            //         {
            //             var password = chain.ResolvePassword(Account, Password);
            //             await txExec.ContractInvokeAsync(script, Account, password, WitnessScope, AdditionalGas);
            //         }

            //         return 0;
            //     }
            //     catch (Exception ex)
            //     {
            //         app.WriteException(ex, showInnerExceptions: true);
            //         return 1;
            //     }
            // }
        }
    }
}
