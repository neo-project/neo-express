// Copyright (C) 2015-2024 The Neo Project.
//
// PolicyCommand.IsBlocked.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.Wallets;
using System.ComponentModel.DataAnnotations;

namespace NeoExpress.Commands
{
    partial class PolicyCommand
    {
        [Command("isBlocked", "blocked", Description = "Unblock account for usage")]
        internal class IsBlocked
        {
            readonly ExpressChainManagerFactory chainManagerFactory;

            public IsBlocked(ExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, Description = "Account to check block status of")]
            [Required]
            internal string ScriptHash { get; init; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    using var expressNode = chainManager.GetExpressNode();

                    var scriptHash = await expressNode.ParseScriptHashToBlockAsync(chainManager.Chain, ScriptHash).ConfigureAwait(false);
                    if (scriptHash.IsT1)
                    {
                        throw new Exception($"{ScriptHash} script hash not found or not supported");
                    }

                    var isBlocked = await expressNode.GetIsBlockedAsync(scriptHash.AsT0).ConfigureAwait(false);
                    await console.Out.WriteLineAsync($"{ScriptHash} account is {(isBlocked ? "" : "not ")}blocked");
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
