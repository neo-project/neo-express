// Copyright (C) 2015-2026 The Neo Project.
//
// ShowTransactionHashTests.cs file belongs to neo-express project and is free
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

public class ShowTransactionHashTests
{
    [Fact]
    public void TryParseTransactionHash_accepts_a_valid_hash()
    {
        var hex = new string('0', 64);
        ShowCommand.Transaction.TryParseTransactionHash(hex, out var hash).Should().BeTrue();
        hash.Should().Be(UInt256.Zero);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("0xzz")]
    public void TryParseTransactionHash_rejects_invalid_input(string value)
    {
        ShowCommand.Transaction.TryParseTransactionHash(value, out _).Should().BeFalse();
    }
}
