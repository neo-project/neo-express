// Copyright (C) 2015-2024 The Neo Project.
//
// ShowCommand.Balance.cs file belongs to neo-express project and is free
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
    partial class ShowCommand
    {
        [Command("balance", Description = "Show asset balance for account")]
        internal class Balance
        {
            readonly ExpressChainManagerFactory chainManagerFactory;

            public Balance(ExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, Description = "Asset to show balance of (symbol or script hash)")]
            [Required]
            internal string Asset { get; init; } = string.Empty;

            [Argument(1, Description = "Account to show asset balance for")]
            [Required]
            internal string Account { get; init; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    using var expressNode = chainManager.GetExpressNode();

                    var getHashResult = await expressNode.TryGetAccountHashAsync(chainManager.Chain, Account).ConfigureAwait(false);
                    if (getHashResult.TryPickT1(out _, out var accountHash))
                    {
                        throw new Exception($"{Account} account not found.");
                    }

                    var (balance, contract) = await expressNode.GetBalanceAsync(accountHash, Asset).ConfigureAwait(false);
                    await console.Out.WriteLineAsync($"{contract.Symbol} ({contract.ScriptHash})\n  balance: {balance.ToBigDecimal(contract.Decimals)}");
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
