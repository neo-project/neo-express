// Copyright (C) 2015-2026 The Neo Project.
//
// ShowCommand.Balances.cs file belongs to neo-express project and is free
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
using System.Numerics;

namespace NeoExpress.Commands
{
    partial class ShowCommand
    {
        [Command("balances", Description = "Show all NEP-17 asset balances for an account")]
        internal class Balances
        {
            readonly ExpressChainManagerFactory chainManagerFactory;

            public Balances(ExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, Description = "Account to show asset balances for (Format: Script Hash, Address, Wallet name)")]
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

                    var balances = await expressNode.ListBalancesAsync(accountHash).ConfigureAwait(false);

                    if (Json)
                    {
                        using var writer = new JsonTextWriter(console.Out) { Formatting = Formatting.Indented };
                        WriteBalancesJson(writer, balances);
                        return 0;
                    }

                    if (balances.Count == 0)
                    {
                        console.WriteLine($"No balances for {Account}");
                    }

                    for (int i = 0; i < balances.Count; i++)
                    {
                        console.WriteLine($"{balances[i].contract.Symbol} ({balances[i].contract.ScriptHash})");
                        console.WriteLine($"  balance: {new BigDecimal(balances[i].balance, balances[i].contract.Decimals)}");
                    }

                    return 0;
                }
                catch (Exception ex)
                {
                    app.WriteException(ex);
                    return 1;
                }
            }

            internal static void WriteBalancesJson(JsonTextWriter writer, IReadOnlyList<(TokenContract contract, BigInteger balance)> balances)
            {
                writer.WriteStartArray();
                for (int i = 0; i < balances.Count; i++)
                {
                    var (contract, balance) = balances[i];
                    writer.WriteStartObject();
                    writer.WritePropertyName("symbol");
                    writer.WriteValue(contract.Symbol);
                    writer.WritePropertyName("script-hash");
                    writer.WriteValue(contract.ScriptHash.ToString());
                    writer.WritePropertyName("decimals");
                    writer.WriteValue(contract.Decimals);
                    writer.WritePropertyName("balance");
                    writer.WriteValue($"{new BigDecimal(balance, contract.Decimals)}");
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
        }
    }
}
