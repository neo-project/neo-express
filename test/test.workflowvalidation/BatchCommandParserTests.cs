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
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Commands;
using Xunit;

namespace test.workflowvalidation;

public class BatchCommandParserTests
{
    [Fact]
    public void Contract_update_is_a_recognized_batch_subcommand()
    {
        var app = new CommandLineApplication<BatchCommand.BatchFileCommands>();
        app.Conventions.UseDefaultConventions();

        var result = app.Parse("contract", "update", "myContract", "contract.nef", "alice");

        result.SelectedCommand.Should()
            .BeOfType<CommandLineApplication<BatchCommand.BatchFileCommands.Contract.Update>>();
    }

    [Fact]
    public void Contract_update_binds_the_data_option()
    {
        var app = new CommandLineApplication<BatchCommand.BatchFileCommands>();
        app.Conventions.UseDefaultConventions();

        var result = app.Parse("contract", "update", "myContract", "contract.nef", "alice", "--data", "42");

        var update = result.SelectedCommand.Should()
            .BeOfType<CommandLineApplication<BatchCommand.BatchFileCommands.Contract.Update>>().Subject;
        update.Model.Data.Should().Be("42");
    }

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
