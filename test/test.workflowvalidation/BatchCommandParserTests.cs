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
    public void Batch_line_errors_include_the_line_number_and_text()
    {
        var original = new FormatException("Unbalanced quote in batch command line.");

        var wrapped = BatchCommand.CreateBatchLineException(2, "wallet create \"alice", original);

        wrapped.Message.Should().Be("Error in batch file line 2: \"wallet create \"alice\" - Unbalanced quote in batch command line.");
        wrapped.InnerException.Should().BeSameAs(original);
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

    [Fact]
    public void Contract_invoke_binds_results_and_does_not_require_account()
    {
        var app = new CommandLineApplication<BatchCommand.BatchFileCommands>();
        app.Conventions.UseDefaultConventions();

        var result = app.Parse("contract", "invoke", "invoke.json", "--results");

        var invoke = result.SelectedCommand.Should()
            .BeOfType<CommandLineApplication<BatchCommand.BatchFileCommands.Contract.Invoke>>().Subject;
        invoke.Model.Results.Should().BeTrue();
        result.SelectedCommand.GetValidationResult()
            .Should().Be(System.ComponentModel.DataAnnotations.ValidationResult.Success);
    }

    [Fact]
    public void Contract_run_binds_additional_gas()
    {
        var app = new CommandLineApplication<BatchCommand.BatchFileCommands>();
        app.Conventions.UseDefaultConventions();

        var result = app.Parse("contract", "run", "myContract", "myMethod", "--account", "alice", "--gas", "5");

        var run = result.SelectedCommand.Should()
            .BeOfType<CommandLineApplication<BatchCommand.BatchFileCommands.Contract.Run>>().Subject;
        run.Model.AdditionalGas.Should().Be(5m);
        run.Model.Account.Should().Be("alice");
    }

    [Fact]
    public void Contract_download_does_not_require_rpc_uri_or_height()
    {
        // The standalone contract download leaves the RPC URI optional and treats
        // height 0 as "latest"; the batch form must match.
        var app = new CommandLineApplication<BatchCommand.BatchFileCommands>();
        app.Conventions.UseDefaultConventions();

        var result = app.Parse("contract", "download", "0x0102030405060708090001020304050607080900");

        result.SelectedCommand.GetValidationResult()
            .Should().Be(System.ComponentModel.DataAnnotations.ValidationResult.Success);
    }

    [Theory]
    [InlineData("register")]
    [InlineData("unregister")]
    [InlineData("unvote")]
    public void Candidate_account_commands_are_recognized_batch_subcommands(string verb)
    {
        var app = new CommandLineApplication<BatchCommand.BatchFileCommands>();
        app.Conventions.UseDefaultConventions();

        var result = app.Parse("candidate", verb, "alice");

        result.SelectedCommand.GetValidationResult().Should().Be(ValidationResult.Success);
    }

    [Fact]
    public void Candidate_vote_requires_a_public_key()
    {
        var app = new CommandLineApplication<BatchCommand.BatchFileCommands>();
        app.Conventions.UseDefaultConventions();

        var missingKey = app.Parse("candidate", "vote", "alice");
        missingKey.SelectedCommand.GetValidationResult().Should().NotBe(ValidationResult.Success);

        var complete = app.Parse("candidate", "vote", "alice", "02158c68fa3a03dbc73a6e20b4bbe626ecb02e765c72b95bd8c2c8f6b52f4a95b0");
        complete.SelectedCommand.GetValidationResult().Should().Be(ValidationResult.Success);
        var model = ((CommandLineApplication<BatchCommand.BatchFileCommands.Candidate.Vote>)complete.SelectedCommand).Model;
        model.Account.Should().Be("alice");
        model.PublicKey.Should().StartWith("02158c");
    }

    [Fact]
    public void Execute_is_a_recognized_batch_subcommand()
    {
        var app = new CommandLineApplication<BatchCommand.BatchFileCommands>();
        app.Conventions.UseDefaultConventions();

        var result = app.Parse("execute", "0c14aa", "--account", "genesis", "--gas", "1.5");

        result.SelectedCommand.GetValidationResult().Should().Be(ValidationResult.Success);
        var model = ((CommandLineApplication<BatchCommand.BatchFileCommands.Execute>)result.SelectedCommand).Model;
        model.InputText.Should().Be("0c14aa");
        model.Account.Should().Be("genesis");
        model.AdditionalGas.Should().Be(1.5m);
        model.Results.Should().BeFalse();
    }

    [Fact]
    public void Execute_requires_a_script_argument()
    {
        var app = new CommandLineApplication<BatchCommand.BatchFileCommands>();
        app.Conventions.UseDefaultConventions();

        var result = app.Parse("execute");

        result.SelectedCommand.GetValidationResult().Should().NotBe(ValidationResult.Success);
    }
}
