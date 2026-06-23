// Copyright (C) 2015-2026 The Neo Project.
//
// ParseParameterIntegerTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit;
using Neo.SmartContract;
using Newtonsoft.Json.Linq;
using System.Numerics;
using Xunit;

namespace test.bctklib
{
    public class ParseParameterIntegerTests
    {
        [Theory]
        [InlineData("42")]
        [InlineData("9223372036854775807")]   // long.MaxValue
        [InlineData("9223372036854775808")]   // long.MaxValue + 1
        [InlineData("18446744073709551615")]  // ulong.MaxValue
        [InlineData("-9223372036854775809")]  // long.MinValue - 1
        public void ParseParameter_handles_integers_outside_long_range(string text)
        {
            var parser = new ContractParameterParser(ProtocolSettings.Default.AddressVersion);

            var param = parser.ParseParameter(JToken.Parse(text));

            param.Type.Should().Be(ContractParameterType.Integer);
            ((BigInteger)param.Value).Should().Be(BigInteger.Parse(text));
        }
    }
}
