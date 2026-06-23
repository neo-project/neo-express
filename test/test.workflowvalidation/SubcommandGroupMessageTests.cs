// Copyright (C) 2015-2026 The Neo Project.
//
// SubcommandGroupMessageTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Commands;
using System;
using System.IO;
using Xunit;

namespace test.workflowvalidation;

public class SubcommandGroupMessageTests
{
    [Fact]
    public void Subcommand_group_prints_a_grammatical_message_when_no_subcommand_is_given()
    {
        var console = new CapturingConsole();
        using var app = new CommandLineApplication<ShowCommand>();
        app.Conventions.UseDefaultConventions();

        var result = new ShowCommand().OnExecute(app, console);

        result.Should().Be(1);
        console.Text.Should().Contain("You must specify a subcommand.");
        console.Text.Should().NotContain("at a subcommand");
    }

    sealed class CapturingConsole : IConsole
    {
        readonly StringWriter writer = new();

        public string Text => writer.ToString();

        public TextWriter Out => writer;
        public TextWriter Error => writer;
        public TextReader In => TextReader.Null;
        public bool IsInputRedirected => true;
        public bool IsOutputRedirected => true;
        public bool IsErrorRedirected => true;
        public ConsoleColor ForegroundColor { get; set; }
        public ConsoleColor BackgroundColor { get; set; }

        public void ResetColor() { }

        public event ConsoleCancelEventHandler? CancelKeyPress { add { } remove { } }
    }
}
