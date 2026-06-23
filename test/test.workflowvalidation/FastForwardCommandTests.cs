// Copyright (C) 2015-2026 The Neo Project.
//
// FastForwardCommandTests.cs file belongs to neo-express project and is free
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

public class FastForwardCommandTests
{
    [Fact]
    public void ValidateCount_rejects_extreme_counts()
    {
        var action = () => FastForwardCommand.ValidateCount(2_147_483_647);

        action.Should().Throw<Exception>()
            .WithMessage($"Cannot mint more than {FastForwardCommand.MaxFastForwardCount} blocks at once");
    }

    [Theory]
    [InlineData("-10")]
    [InlineData("-1.00:00:00")]
    public void ParseTimestampDelta_rejects_negative(string input)
    {
        var action = () => FastForwardCommand.ParseTimestampDelta(input);

        action.Should().Throw<Exception>()
            .WithMessage($"*{input}*");
    }

    [Fact]
    public void ParseTimestampDelta_accepts_valid_inputs()
    {
        FastForwardCommand.ParseTimestampDelta("5").Should().Be(TimeSpan.FromSeconds(5));
        FastForwardCommand.ParseTimestampDelta("").Should().Be(TimeSpan.Zero);
        FastForwardCommand.ParseTimestampDelta("1.00:00:00").Should().Be(TimeSpan.FromDays(1));
    }
}
