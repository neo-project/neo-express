// Copyright (C) 2015-2024 The Neo Project.
//
// PolicyCommand.Set.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Models;
using System.ComponentModel.DataAnnotations;

namespace NeoExpress.Commands
{
    partial class PolicyCommand
    {
        [Command(Name = "set", Description = "Set single policy value")]
        internal class Set
        {
            readonly ExpressChainManagerFactory chainManagerFactory;
            readonly TransactionExecutorFactory txExecutorFactory;

            public Set(ExpressChainManagerFactory chainManagerFactory, TransactionExecutorFactory txExecutorFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
                this.txExecutorFactory = txExecutorFactory;
            }

            [Argument(0, Description = "Policy to set")]
            [Required]
            internal PolicySettings Policy { get; init; }

            [Argument(1, Description = "New Policy Value")]
            [Required]
            internal decimal Value { get; set; }

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

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
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
                    app.WriteException(ex);
                    return 1;
                }
            }
        }
    }
}
