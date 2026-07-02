// Copyright (C) 2015-2026 The Neo Project.
//
// ShowCommand.Balance.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo;
using NeoExpress.Models;
using Newtonsoft.Json;
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

            [Argument(1, Description = "Account to show asset balance for (Format: Script Hash, Address, Wallet name)")]
            [Required]
            internal string Account { get; init; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            [Option(Description = "Output as JSON")]
            internal bool Json { get; init; } = false;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    using var expressNode = chainManager.GetExpressNode();

                    if (!UInt160.TryParse(Account, out var accountHash))
                    {
                        var getHashResult = await expressNode.TryGetAccountHashAsync(chainManager.Chain, Account).ConfigureAwait(false);
                        if (getHashResult.TryPickT1(out _, out accountHash))
                        {
                            throw new Exception($"{Account} account not found.");
                        }
                    }

                    var (balance, contract) = await expressNode.GetBalanceAsync(accountHash, Asset).ConfigureAwait(false);
                    if (Json)
                    {
                        using var writer = new JsonTextWriter(console.Out) { Formatting = Formatting.Indented };
                        WriteBalanceJson(writer, contract, balance.ToBigDecimal(contract.Decimals));
                    }
                    else
                    {
                        await console.Out.WriteLineAsync($"{contract.Symbol} ({contract.ScriptHash})\n  balance: {balance.ToBigDecimal(contract.Decimals)}");
                    }
                    return 0;
                }
                catch (Exception ex)
                {
                    app.WriteException(ex);
                    return 1;
                }
            }

            internal static void WriteBalanceJson(JsonTextWriter writer, Nep17Contract contract, BigDecimal balance)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("symbol");
                writer.WriteValue(contract.Symbol);
                writer.WritePropertyName("script-hash");
                writer.WriteValue(contract.ScriptHash.ToString());
                writer.WritePropertyName("decimals");
                writer.WriteValue(contract.Decimals);
                writer.WritePropertyName("balance");
                writer.WriteValue($"{balance}");
                writer.WriteEndObject();
            }
        }
    }
}
