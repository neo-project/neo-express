// Copyright (C) 2015-2026 The Neo Project.
//
// NotifyEventArgsAssertionsTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.Assertions;
using Neo.SmartContract;
using Xunit;
using NeoArray = Neo.VM.Types.Array;

namespace test.assertions;

public class NotifyEventArgsAssertionsTests
{
    static readonly UInt160 HashA = UInt160.Parse("0x0102030405060708090a0b0c0d0e0f1011121314");
    static readonly UInt160 HashB = UInt160.Parse("0x1414131211100f0e0d0c0b0a0908070605040302");

    static NotifyEventArgs Make(UInt160 scriptHash, string eventName) =>
        new NotifyEventArgs(null!, scriptHash, eventName, new NeoArray());

    [Fact]
    public void HaveEventName_matches()
    {
        var act = () => Make(HashA, "Transfer").Should().HaveEventName("Transfer");

        act.Should().NotThrow();
    }

    [Fact]
    public void HaveEventName_reports_mismatch()
    {
        var act = () => Make(HashA, "Transfer").Should().HaveEventName("Burn");

        act.Should().Throw<Xunit.Sdk.XunitException>()
            .WithMessage("*Transfer*");
    }

    [Fact]
    public void HaveEventName_includes_the_supplied_reason_in_the_failure_message()
    {
        var act = () => Make(HashA, "Transfer").Should().HaveEventName("Burn", "of {0}", "the expected event");

        act.Should().Throw<Xunit.Sdk.XunitException>()
            .WithMessage("*because of the expected event*");
    }

    [Fact]
    public void NotBeSentBy_passes_for_a_different_hash()
    {
        var act = () => Make(HashA, "Transfer").Should().NotBeSentBy(HashB);

        act.Should().NotThrow();
    }

    [Fact]
    public void NotBeSentBy_reports_a_matching_hash()
    {
        var act = () => Make(HashA, "Transfer").Should().NotBeSentBy(HashA);

        act.Should().Throw<Xunit.Sdk.XunitException>()
            .WithMessage("*not to be sent by*");
    }
}
