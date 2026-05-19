// Copyright (C) 2015-2026 The Neo Project.
//
// StackItemAssertionsTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.Assertions;
using Neo.Extensions;
using Neo.VM.Types;
using System.Numerics;
using Xunit;
using NeoArray = Neo.VM.Types.Array;

namespace test.assertions;

public class StackItemAssertionsTests
{
    [Fact]
    public void BeEquivalentTo_string_matches_byte_string_value()
    {
        StackItem item = (ByteString)"hello";

        var act = () => item.Should().BeEquivalentTo("hello");

        act.Should().NotThrow();
    }

    [Fact]
    public void BeEquivalentTo_string_reports_mismatch()
    {
        StackItem item = (ByteString)"hello";

        var act = () => item.Should().BeEquivalentTo("world");

        act.Should().Throw<Xunit.Sdk.XunitException>()
            .WithMessage("*hello*");
    }

    [Fact]
    public void BeEquivalentTo_BigInteger_matches_integer_value()
    {
        StackItem item = (Integer)42;

        var act = () => item.Should().BeEquivalentTo(new BigInteger(42));

        act.Should().NotThrow();
    }

    [Fact]
    public void BeEquivalentTo_bool_matches_boolean_value()
    {
        StackItem item = StackItem.True;

        var act = () => item.Should().BeEquivalentTo(true);

        act.Should().NotThrow();
    }

    [Fact]
    public void BeTrue_and_BeFalse_use_boolean_value()
    {
        StackItem t = StackItem.True;
        StackItem f = StackItem.False;

        t.Should().BeTrue();
        f.Should().BeFalse();
    }

    [Fact]
    public void BeEquivalentTo_null_matches_null_stack_item()
    {
        StackItem item = StackItem.Null;

        var act = () => item.Should().BeEquivalentTo((object?)null);

        act.Should().NotThrow();
    }

    [Fact]
    public void BeEquivalentTo_null_reports_non_null_subject()
    {
        StackItem item = (Integer)1;

        var act = () => item.Should().BeEquivalentTo((object?)null);

        act.Should().Throw<Xunit.Sdk.XunitException>();
    }

    [Fact]
    public void BeEquivalentTo_UInt160_matches_byte_string_of_correct_length()
    {
        var hash = UInt160.Parse("0x0102030405060708090a0b0c0d0e0f1011121314");
        StackItem item = (ByteString)hash.ToArray();

        var act = () => item.Should().BeEquivalentTo(hash);

        act.Should().NotThrow();
    }

    [Fact]
    public void BeEquivalentTo_UInt160_reports_length_mismatch()
    {
        var hash = UInt160.Parse("0x0102030405060708090a0b0c0d0e0f1011121314");
        StackItem item = (ByteString)new byte[10];

        var act = () => item.Should().BeEquivalentTo(hash);

        act.Should().Throw<Xunit.Sdk.XunitException>();
    }

    [Fact]
    public void BeEquivalentTo_UInt256_matches_byte_string_of_correct_length()
    {
        var hash = UInt256.Parse("0x0101010101010101010101010101010101010101010101010101010101010101");
        StackItem item = (ByteString)hash.ToArray();

        var act = () => item.Should().BeEquivalentTo(hash);

        act.Should().NotThrow();
    }

    [Fact]
    public void BeEquivalentTo_byte_array_matches_byte_string_contents()
    {
        var expected = new byte[] { 0xde, 0xad, 0xbe, 0xef };
        StackItem item = (ByteString)expected;

        var act = () => item.Should().BeEquivalentTo(expected);

        act.Should().NotThrow();
    }

    [Fact]
    public void BeEquivalentTo_byte_array_reports_content_mismatch()
    {
        StackItem item = (ByteString)new byte[] { 0xde, 0xad };
        var expected = new byte[] { 0xbe, 0xef };

        var act = () => item.Should().BeEquivalentTo(expected);

        act.Should().Throw<Xunit.Sdk.XunitException>();
    }

    [Fact]
    public void BeEquivalentTo_unsupported_type_reports_failure()
    {
        StackItem item = (Integer)1;
        var unsupported = new NeoArray();

        var act = () => item.Should().BeEquivalentTo((object)unsupported);

        act.Should().Throw<Xunit.Sdk.XunitException>()
            .WithMessage("*Unknown*");
    }
}
