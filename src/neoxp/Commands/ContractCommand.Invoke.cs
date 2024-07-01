// Copyright (C) 2015-2024 The Neo Project.
//
// ContractCommand.Invoke.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P.Payloads;
using System.ComponentModel.DataAnnotations;

namespace NeoExpress.Commands
{
    using McMaster.Extensions.CommandLineUtils;

    partial class ContractCommand
    {
        [Command(Name = "invoke", Description = "Invoke a contract using parameters from .neo-invoke.json file")]
        internal class Invoke
        {
            readonly ExpressChainManagerFactory chainManagerFactory;
            readonly TransactionExecutorFactory txExecutorFactory;

            public Invoke(ExpressChainManagerFactory chainManagerFactory, TransactionExecutorFactory txExecutorFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
                this.txExecutorFactory = txExecutorFactory;
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

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    if (string.IsNullOrEmpty(Account) && !Results)
                    {
                        throw new Exception("Either Account or --results must be specified");
                    }

                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    using var txExec = txExecutorFactory.Create(chainManager, Trace, Json);
                    var script = await txExec.LoadInvocationScriptAsync(InvocationFile).ConfigureAwait(false);

                    if (Results)
                    {
                        await txExec.InvokeForResultsAsync(script, Account, WitnessScope);
                    }
                    else
                    {
                        var password = chainManager.Chain.ResolvePassword(Account, Password);
                        await txExec.ContractInvokeAsync(script, Account, password, WitnessScope, AdditionalGas);
                    }

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
