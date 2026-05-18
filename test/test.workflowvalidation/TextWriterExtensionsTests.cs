// Copyright (C) 2015-2026 The Neo Project.
//
// TextWriterExtensionsTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using NeoExpress;
using Newtonsoft.Json.Linq;
using Xunit;

namespace test.workflowvalidation;

public class TextWriterExtensionsTests
{
    [Fact]
    public async Task WriteTxHashAsync_writes_valid_json_string()
    {
        var hash = UInt256.Parse("0x0101010101010101010101010101010101010101010101010101010101010101");
        using var writer = new StringWriter();

        await writer.WriteTxHashAsync(hash, json: true);

        var token = JToken.Parse(writer.ToString());
        token.Type.Should().Be(JTokenType.String);
        token.Value<string>().Should().Be(hash.ToString());
    }
}
