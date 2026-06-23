// Copyright (C) 2015-2026 The Neo Project.
//
// ToNeoJsonIntegerTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.BlockchainToolkit;
using Newtonsoft.Json.Linq;
using System.Numerics;
using Xunit;

namespace test.bctklib
{
    public class ToNeoJsonIntegerTests
    {
        [Theory]
        [InlineData("42")]
        [InlineData("9223372036854775808")]    // long.MaxValue + 1
        [InlineData("18446744073709551615")]   // ulong.MaxValue
        [InlineData("-9223372036854775809")]   // long.MinValue - 1
        [InlineData("-18446744073709551615")]  // below long range, negative
        public void ToNeoJson_converts_integers_outside_long_range(string text)
        {
            var result = JsonWriterExtensions.ToNeoJson(JToken.Parse(text));

            result.Should().BeOfType<Neo.Json.JNumber>();
            result!.GetNumber().Should().Be((double)BigInteger.Parse(text));
        }
    }
}
