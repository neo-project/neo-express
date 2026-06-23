// Copyright (C) 2015-2026 The Neo Project.
//
// ShowStateCommandTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Commands;
using Xunit;

namespace test.workflowvalidation;

public class ShowStateCommandTests
{
    // show state reports chain-level state (block height, running, config file) and takes
    // no positional argument; a stray positional must be rejected, not silently ignored.
    [Fact]
    public void State_does_not_accept_a_positional_argument()
    {
        var app = new CommandLineApplication<ShowCommand.State>();
        app.Conventions.UseDefaultConventions();

        var act = () => app.Parse("0");

        act.Should().Throw<CommandParsingException>();
    }
}
