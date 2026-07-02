// Copyright (C) 2015-2026 The Neo Project.
//
// ShowJsonOutputTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using NeoExpress.Commands;
using NeoExpress.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Numerics;
using Xunit;

namespace test.workflowvalidation;

public class ShowJsonOutputTests
{
    static readonly UInt160 GAS_HASH = UInt160.Parse("0xd2a4cff31913016155e38e474a2c06d08be276cf");

    static string Capture(Action<JsonTextWriter> write)
    {
        using var stringWriter = new StringWriter();
        using var writer = new JsonTextWriter(stringWriter);
        write(writer);
        return stringWriter.ToString();
    }

    [Fact]
    public void show_balance_json_reports_symbol_hash_decimals_and_balance()
    {
        var contract = new Nep17Contract("GAS", 8, GAS_HASH);
        var balance = new BigDecimal(BigInteger.Parse("10050000000"), 8);

        var json = JObject.Parse(Capture(w => ShowCommand.Balance.WriteBalanceJson(w, contract, balance)));

        json.Value<string>("symbol").Should().Be("GAS");
        json.Value<string>("script-hash").Should().Be(GAS_HASH.ToString());
        json.Value<int>("decimals").Should().Be(8);
        json.Value<string>("balance").Should().Be("100.5");
    }

    [Fact]
    public void show_balances_json_reports_an_array_of_balances()
    {
        var balances = new (TokenContract contract, BigInteger balance)[]
        {
            (new TokenContract("GAS", 8, GAS_HASH, TokenStandard.Nep17), BigInteger.Parse("10050000000")),
        };

        var json = JArray.Parse(Capture(w => ShowCommand.Balances.WriteBalancesJson(w, balances)));

        json.Should().HaveCount(1);
        json[0].Value<string>("symbol").Should().Be("GAS");
        json[0].Value<string>("script-hash").Should().Be(GAS_HASH.ToString());
        json[0].Value<int>("decimals").Should().Be(8);
        json[0].Value<string>("balance").Should().Be("100.5");
    }

    [Fact]
    public void show_balances_json_reports_an_empty_array_when_there_are_no_balances()
    {
        var json = JArray.Parse(Capture(w =>
            ShowCommand.Balances.WriteBalancesJson(w, Array.Empty<(TokenContract, BigInteger)>())));

        json.Should().BeEmpty();
    }

    [Fact]
    public void show_nft_json_reports_token_ids_in_base64_and_hex()
    {
        var tokenId = Convert.ToBase64String(new byte[] { 0x0a, 0x0b, 0x0c });

        var json = JArray.Parse(Capture(w => ShowCommand.NFT.WriteTokenIdsJson(w, new[] { tokenId })));

        json.Should().HaveCount(1);
        json[0].Value<string>("token-id-base64").Should().Be(tokenId);
        json[0].Value<string>("token-id-hex").Should().Be("0x0a0b0c");
    }

    [Fact]
    public void show_nft_json_reports_an_empty_array_when_there_are_no_tokens()
    {
        var json = JArray.Parse(Capture(w => ShowCommand.NFT.WriteTokenIdsJson(w, Array.Empty<string>())));

        json.Should().BeEmpty();
    }
}
