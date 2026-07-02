// Copyright (C) 2015-2026 The Neo Project.
//
// ExportPasswordTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using NeoExpress;
using Xunit;

namespace test.workflowvalidation;

public class ExportPasswordTests
{
    [Fact]
    public void a_provided_password_is_used_without_prompting()
    {
        var prompted = false;

        var result = Extensions.ResolveExportPassword("secret", isInputRedirected: true, () => { prompted = true; return "from-prompt"; });

        result.Should().Be("secret");
        prompted.Should().BeFalse();
    }

    [Fact]
    public void an_interactive_session_prompts_when_no_password_is_provided()
    {
        var result = Extensions.ResolveExportPassword(string.Empty, isInputRedirected: false, () => "from-prompt");

        result.Should().Be("from-prompt");
    }

    [Fact]
    public void a_non_interactive_session_fails_clearly_instead_of_blocking_on_a_prompt()
    {
        var prompted = false;

        var act = () => Extensions.ResolveExportPassword(string.Empty, isInputRedirected: true, () => { prompted = true; return "from-prompt"; });

        act.Should().Throw<Exception>().WithMessage("*--password*");
        prompted.Should().BeFalse();
    }
}
