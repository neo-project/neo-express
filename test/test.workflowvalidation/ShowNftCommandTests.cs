// Copyright (C) 2015-2026 The Neo Project.
//
// ShowNftCommandTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using NeoExpress.Commands;
using Xunit;

namespace test.workflowvalidation;

public class ShowNftCommandTests
{
    [Fact]
    public void FormatTokenId_preserves_byte_order_for_hex_display()
    {
        var tokenId = Convert.ToBase64String([0x0a, 0x0b, 0x0c]);

        var result = ShowCommand.NFT.FormatTokenId(tokenId);

        result.Should().Be($"TokenId(Base64): {tokenId}, TokenId(Hex): 0x0a0b0c");
    }
}
