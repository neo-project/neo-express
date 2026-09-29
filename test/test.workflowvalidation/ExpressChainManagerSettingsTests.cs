// Copyright (C) 2015-2026 The Neo Project.
//
// ExpressChainManagerSettingsTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.BlockchainToolkit.Models;
using NeoExpress;
using NeoExpress.Models;
using System;
using Xunit;

namespace test.workflowvalidation;

public class ExpressChainManagerSettingsTests
{
    [Fact]
    public void CreateConsensusSettings_reads_dbft_max_block_system_fee()
    {
        var chain = CreateChain();
        chain.Settings[ExpressChainManager.MaxBlockSystemFeeSetting] = "4000000000";

        var settings = ExpressChainManager.CreateConsensusSettings(chain);

        settings.MaxBlockSystemFee.Should().Be(40_00000000L);
    }

    [Fact]
    public void CreateConsensusSettings_sets_ignore_recovery_logs()
    {
        var settings = ExpressChainManager.CreateConsensusSettings(CreateChain());

        settings.IgnoreRecoveryLogs.Should().BeTrue();
    }

    [Theory]
    [InlineData("127.0.0.2", "http://127.0.0.2:50002/")]
    [InlineData("0.0.0.0", "http://127.0.0.1:50002/")]
    [InlineData("::2", "http://[::2]:50002/")]
    [InlineData("::", "http://[::1]:50002/")]
    public void GetRpcUri_uses_the_configured_bind_address(string bindAddress, string expected)
    {
        var chain = CreateChain();
        chain.Settings["rpc.BindAddress"] = bindAddress;
        var node = new ExpressConsensusNode { RpcPort = 50002 };

        ExpressChainManager.GetRpcUri(chain, node).Should().Be(new Uri(expected));
    }

    [Fact]
    public void GetRpcUri_falls_back_to_loopback_for_invalid_bind_addresses()
    {
        var chain = CreateChain();
        chain.Settings["rpc.BindAddress"] = "not-an-ip-address";
        var node = new ExpressConsensusNode { RpcPort = 50002 };

        ExpressChainManager.GetRpcUri(chain, node).Should().Be(new Uri("http://127.0.0.1:50002/"));
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("-1")]
    public void CreateConsensusSettings_ignores_invalid_dbft_max_block_system_fee(string value)
    {
        var defaultSettings = ExpressChainManager.CreateConsensusSettings(CreateChain());
        var chain = CreateChain();
        chain.Settings[ExpressChainManager.MaxBlockSystemFeeSetting] = value;

        var settings = ExpressChainManager.CreateConsensusSettings(chain);

        settings.MaxBlockSystemFee.Should().Be(defaultSettings.MaxBlockSystemFee);
    }

    static ExpressChain CreateChain()
        => new()
        {
            Network = 12345
        };
}
