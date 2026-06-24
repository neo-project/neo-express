// Copyright (C) 2015-2026 The Neo Project.
//
// ContractValidateTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using NeoExpress.Commands;
using System;
using System.IO;
using Xunit;

namespace test.workflowvalidation;

public class ContractValidateTests
{
    static readonly UInt160 Hash = UInt160.Parse("0x0102030405060708090a0b0c0d0e0f1011121314");

    [Fact]
    public void ComplianceResult_reports_success_for_a_compliant_contract()
    {
        var (message, exitCode) = ContractCommand.Validate.ComplianceResult(Hash, "NEP-17", true);

        message.Should().Be($"{Hash} is NEP-17 compliant.");
        exitCode.Should().Be(0);
    }

    [Fact]
    public void ComplianceResult_reports_failure_for_a_noncompliant_contract()
    {
        var (message, exitCode) = ContractCommand.Validate.ComplianceResult(Hash, "NEP-11", false);

        message.Should().Be($"{Hash} is NOT NEP-11 compliant.");
        exitCode.Should().NotBe(0);
    }

    [Fact]
    public async Task WriteMessageAsync_skips_empty_messages()
    {
        var console = new CapturingConsole();

        await ContractCommand.Validate.WriteMessageAsync(console, string.Empty);
        await ContractCommand.Validate.WriteMessageAsync(console, "   ");

        console.Text.Should().BeEmpty();
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
