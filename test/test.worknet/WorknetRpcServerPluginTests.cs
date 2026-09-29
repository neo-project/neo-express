// Copyright (C) 2015-2026 The Neo Project.
//
// WorknetRpcServerPluginTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit.Plugins;
using Neo.Json;
using System.Numerics;
using Xunit;

namespace test.worknet;

public class WorknetRpcServerPluginTests
{
    [Fact]
    public void GetNep11Balances_uses_the_standard_tokens_property()
    {
        var balance = new ToolkitRpcServer.Nep11Balance(
            UInt160.Zero,
            "Example NFT",
            "NFT",
            0,
            [new ToolkitRpcServer.Nep11TokenBalance(new byte[] { 0x0a, 0x0b }, BigInteger.One, 7)]);

        var json = WorknetRpcServerPlugin.CreateNep11BalanceJson(balance);

        json["tokens"].Should().BeOfType<JArray>();
        json["token"].Should().BeNull();
        ((JArray)json["tokens"]!)[0]!["tokenid"]!.AsString().Should().Be("0a0b");
    }
}
