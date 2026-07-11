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
