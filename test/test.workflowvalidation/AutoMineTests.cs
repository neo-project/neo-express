// Copyright (C) 2015-2026 The Neo Project.
//
// AutoMineTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using NeoExpress;
using NeoExpress.Node;
using Xunit;

namespace test.workflowvalidation;

public class AutoMineTests
{
    [Fact]
    public void auto_mine_is_off_by_default()
    {
        var chain = ExpressChainManagerFactory.CreateChain(1, null);

        OnlineNode.ShouldAutoMine(chain).Should().BeFalse();
    }

    [Fact]
    public void auto_mine_is_on_for_a_single_node_chain_with_the_setting()
    {
        var chain = ExpressChainManagerFactory.CreateChain(1, null);
        chain.Settings["chain.AutoMine"] = "true";

        OnlineNode.ShouldAutoMine(chain).Should().BeTrue();
    }

    [Fact]
    public void auto_mine_is_off_for_a_multi_node_chain_even_with_the_setting()
    {
        var chain = ExpressChainManagerFactory.CreateChain(4, null);
        chain.Settings["chain.AutoMine"] = "true";

        OnlineNode.ShouldAutoMine(chain).Should().BeFalse();
    }

    [Theory]
    [InlineData("false")]
    [InlineData("0")]
    [InlineData("yes")]
    [InlineData("")]
    public void auto_mine_is_off_for_a_disabled_or_invalid_setting(string value)
    {
        var chain = ExpressChainManagerFactory.CreateChain(1, null);
        chain.Settings["chain.AutoMine"] = value;

        OnlineNode.ShouldAutoMine(chain).Should().BeFalse();
    }
}
