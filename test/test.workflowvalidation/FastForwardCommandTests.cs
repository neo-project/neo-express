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
}
