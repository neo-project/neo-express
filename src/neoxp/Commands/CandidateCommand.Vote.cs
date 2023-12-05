// Copyright (C) 2015-2023 The Neo Project.
//
// CandidateCommand.Vote.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using System.ComponentModel.DataAnnotations;

namespace NeoExpress.Commands
{
    partial class CandidateCommand
    {
        [Command(Name = "vote", Description = "Vote candidate")]
        internal class Vote
        {
            readonly ExpressChainManagerFactory chainManagerFactory;
            readonly TransactionExecutorFactory txExecutorFactory;

            public Vote(ExpressChainManagerFactory chainManagerFactory, TransactionExecutorFactory txExecutorFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
                this.txExecutorFactory = txExecutorFactory;
            }

            [Argument(0, Description = "Account to vote")]
            [Required]
            internal string Account { get; init; } = string.Empty;

            [Argument(1, Description = "Candidate publickey")]
            [Required]
            internal string PublicKey { get; init; } = string.Empty;

            [Option(Description = "Password to use for NEP-2/NEP-6 account")]
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
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    var password = chainManager.Chain.ResolvePassword(Account, Password);
                    using var txExec = txExecutorFactory.Create(chainManager, Trace, Json);
                    await txExec.VoteAsync(Account, PublicKey, password).ConfigureAwait(false);
                    return 0;
                }
                catch (Exception ex)
                {
                    app.WriteException(ex, true);
                    return 1;
                }
            }
        }
    }
}
