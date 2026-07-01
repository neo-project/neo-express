// Copyright (C) 2015-2026 The Neo Project.
//
// Base64FixedTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using NeoExpress;
using Xunit;

namespace test.workflowvalidation;

public class Base64FixedTests
{
    [Fact]
    public void Base64Fixed_decodes_a_hex_unicode_escape()
    {
        // "+" is the escaped form of '+' in a Base64 string.
        var input = "DCECbzTesnBofh/Xng1SofChKkBC7jhVmLxCN1vk" + "\\u002B" + "49xa2pBVuezJw==";

        var result = Extensions.Base64Fixed(input);

        result.Should().Be("DCECbzTesnBofh/Xng1SofChKkBC7jhVmLxCN1vk+49xa2pBVuezJw==");
    }

    [Fact]
    public void Base64Fixed_leaves_a_non_hex_unicode_sequence_unchanged()
    {
        // The escape characters are word characters but not hexadecimal digits. The previous
        // \w-based pattern matched them and then threw when parsing them as hex.
        const string input = @"abc\uZZZZdef";

        var result = Extensions.Base64Fixed(input);

        result.Should().Be(input);
    }

    [Fact]
    public void TryGetBytesFromBase64String_returns_false_for_a_non_hex_unicode_sequence()
    {
        // A Try method must not throw for malformed input.
        var act = () => @"\uZZZZ".TryGetBytesFromBase64String(out _);

        act.Should().NotThrow().Which.Should().BeFalse();
    }
}
