// Copyright (C) 2015-2026 The Neo Project.
//
// StorageItemAssertionsTests.cs file belongs to neo-express project and is free
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
using Neo.SmartContract;
using System.Numerics;
using Xunit;

namespace test.assertions;

public class StorageItemAssertionsTests
{
    [Fact]
    public void Be_BigInteger_matches_serialized_value()
    {
        var expected = new BigInteger(123456);
        var item = new StorageItem(expected.ToByteArray());

        var act = () => item.Should().Be(expected);

        act.Should().NotThrow();
    }

    [Fact]
    public void Be_BigInteger_reports_mismatch()
    {
        var item = new StorageItem(new BigInteger(1).ToByteArray());

        var act = () => item.Should().Be(new BigInteger(2));

        act.Should().Throw<Xunit.Sdk.XunitException>()
            .WithMessage("*2*");
    }

    [Fact]
    public void Be_includes_the_supplied_reason_in_the_failure_message()
    {
        var item = new StorageItem(new BigInteger(1).ToByteArray());

        var act = () => item.Should().Be(new BigInteger(2), "of {0}", "the configured value");

        act.Should().Throw<Xunit.Sdk.XunitException>()
            .WithMessage("*because of the configured value*");
    }

    [Fact]
    public void Be_UInt160_matches_raw_bytes()
    {
        var hash = UInt160.Parse("0x0102030405060708090a0b0c0d0e0f1011121314");
        var item = new StorageItem(hash.ToArray());

        var act = () => item.Should().Be(hash);

        act.Should().NotThrow();
    }

    [Fact]
    public void Be_UInt256_matches_raw_bytes()
    {
        var hash = UInt256.Parse("0x0202020202020202020202020202020202020202020202020202020202020202");
        var item = new StorageItem(hash.ToArray());

        var act = () => item.Should().Be(hash);

        act.Should().NotThrow();
    }

    [Fact]
    public void Be_byte_array_matches_raw_value()
    {
        var item = new StorageItem(new byte[] { 0xde, 0xad, 0xbe, 0xef });

        var act = () => item.Should().Be(new byte[] { 0xde, 0xad, 0xbe, 0xef });

        act.Should().NotThrow();
    }

    [Fact]
    public void Be_byte_array_matches_an_empty_value()
    {
        var item = new StorageItem(System.Array.Empty<byte>());

        var act = () => item.Should().Be(System.Array.Empty<byte>());

        act.Should().NotThrow();
    }

    [Fact]
    public void Be_byte_array_reports_a_null_expected_value()
    {
        var item = new StorageItem(new byte[] { 0x01, 0x02 });

        var act = () => item.Should().Be((byte[])null!);

        act.Should().Throw<Xunit.Sdk.XunitException>()
            .WithMessage("*expected*null*");
    }

    [Fact]
    public void Be_byte_array_reports_mismatch()
    {
        var item = new StorageItem(new byte[] { 0x01, 0x02 });

        var act = () => item.Should().Be(new byte[] { 0x03, 0x04 });

        act.Should().Throw<Xunit.Sdk.XunitException>()
            .WithMessage("*0304*");
    }

    [Fact]
    public void Be_byte_array_includes_the_supplied_reason_in_the_failure_message()
    {
        var item = new StorageItem(new byte[] { 0x01, 0x02 });

        var act = () => item.Should().Be(new byte[] { 0x03, 0x04 }, "of {0}", "the configured value");

        act.Should().Throw<Xunit.Sdk.XunitException>()
            .WithMessage("*because of the configured value*");
    }
}
