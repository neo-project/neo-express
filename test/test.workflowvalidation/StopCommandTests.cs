// Copyright (C) 2015-2026 The Neo Project.
//
// StopCommandTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using NeoExpress.Commands;
using Xunit;

namespace test.workflowvalidation;

public class StopCommandTests
{
    [Fact]
    public void ResolveNodeIndex_requires_an_index_for_a_multi_node_chain()
    {
        var action = () => StopCommand.ResolveNodeIndex(null, nodeCount: 4);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("node index or --all must be specified when stopping a multi-node chain");
    }

    [Fact]
    public void ResolveNodeIndex_defaults_to_zero_for_a_single_node_chain()
    {
        StopCommand.ResolveNodeIndex(null, nodeCount: 1).Should().Be(0);
    }

    [Fact]
    public void ResolveNodeIndex_returns_the_supplied_index_when_in_range()
    {
        StopCommand.ResolveNodeIndex(2, nodeCount: 4).Should().Be(2);
    }

    [Fact]
    public void ResolveNodeIndex_rejects_an_out_of_range_index()
    {
        var action = () => StopCommand.ResolveNodeIndex(7, nodeCount: 4);

        action.Should().Throw<ArgumentException>();
    }
}
