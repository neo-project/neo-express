// Copyright (C) 2015-2024 The Neo Project.
//
// ContractCommand.Update.cs file belongs to neo-express project and is free
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
        [Command("update", Description = "update a contract that has been deployed to a neo-express instance")]
        internal class Update
        {
            readonly ExpressChainManagerFactory chainManagerFactory;
            readonly TransactionExecutorFactory txExecutorFactory;

            public Update(ExpressChainManagerFactory chainManagerFactory, TransactionExecutorFactory txExecutorFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
                this.txExecutorFactory = txExecutorFactory;
            }

            [Argument(0, Description = "Contract name or invocation hash")]
            [Required]
            internal string Contract { get; init; } = string.Empty;

            [Argument(1, Description = "Path to contract .nef file")]
            [Required]
            internal string NefFile { get; init; } = string.Empty;

            [Argument(2, Description = "Account to pay contract deployment GAS fee")]
            [Required]
            internal string Account { get; init; } = string.Empty;

            [Option(Description = "Data parameter for update method on contract (Format: JSON)")]
            internal string Data { get; init; } = string.Empty;

            [Option(Description = "Witness Scope to use for transaction signer (Default: CalledByEntry)")]
            [AllowedValues(StringComparison.OrdinalIgnoreCase, "None", "CalledByEntry", "Global")]
            internal WitnessScope WitnessScope { get; init; } = WitnessScope.CalledByEntry;

            [Option(Description = "Password to use for NEP-2/NEP-6 account")]
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
                    var password = chainManager.Chain.ResolvePassword(Account, Password);
                    using var txExec = txExecutorFactory.Create(chainManager, Trace, Json);
                    var data = txExec.ContractParameterParser(Data);
                    await txExec.ContractUpdateAsync(Contract, NefFile, Account, password, WitnessScope, data).ConfigureAwait(false);
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
