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
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace test.workflowvalidation;

public class BatchCommandParserTests
{
    [Fact]
    public void Validation_detects_a_batch_line_missing_a_required_argument()
    {
        var app = new CommandLineApplication<BatchCommand.BatchFileCommands>();
        app.Conventions.UseDefaultConventions();

        // transfer requires a receiver; this line omits it.
        var result = app.Parse("transfer", "10", "gas", "alice");

        result.SelectedCommand.GetValidationResult().Should().NotBe(ValidationResult.Success);
    }

    [Fact]
    public void Validation_passes_for_a_complete_batch_line()
    {
        var app = new CommandLineApplication<BatchCommand.BatchFileCommands>();
        app.Conventions.UseDefaultConventions();

        var result = app.Parse("transfer", "10", "gas", "alice", "bob");

        result.SelectedCommand.GetValidationResult().Should().Be(ValidationResult.Success);
    }

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

    [Fact]
    public void SplitCommandLine_keeps_text_after_a_mid_word_quote_in_the_same_token()
    {
        BatchCommand.SplitCommandLine("foo\"bar\"baz").Should()
            .Equal("foobarbaz");
    }

    [Fact]
    public void SplitCommandLine_joins_quoted_segments_within_a_single_word()
    {
        BatchCommand.SplitCommandLine("a\"b\"c").Should()
            .Equal("abc");
    }

    [Fact]
    public void SplitCommandLine_strips_every_quote_from_a_single_token()
    {
        // A token carrying more than one quoted segment has all of its quote
        // characters removed, so a word with two quote pairs collapses to a
        // single unquoted token.
        BatchCommand.SplitCommandLine("a\"b\"c\"d\"").Should()
            .Equal("abcd");
    }
}
