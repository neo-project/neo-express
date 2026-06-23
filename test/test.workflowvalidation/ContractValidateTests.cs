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
using Neo;
using NeoExpress.Commands;
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
}
