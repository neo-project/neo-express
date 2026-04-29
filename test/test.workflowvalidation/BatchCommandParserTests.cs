// Copyright (C) 2015-2026 The Neo Project.
//
// BatchCommandParserTests.cs file belongs to neo-express project and is free
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

public class BatchCommandParserTests
{
    [Fact]
    public void SplitCommandLine_reports_unbalanced_quotes()
    {
        var act = () => BatchCommand.SplitCommandLine("wallet create \"alice").ToArray();

        act.Should().Throw<FormatException>()
            .WithMessage("Unbalanced quote in batch command line.");
    }

    [Fact]
    public void SplitCommandLine_preserves_balanced_quoted_tokens()
    {
        BatchCommand.SplitCommandLine("wallet create \"alice bob\"").Should()
            .Equal("wallet", "create", "alice bob");
    }
}
