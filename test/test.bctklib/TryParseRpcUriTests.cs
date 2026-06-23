// Copyright (C) 2015-2026 The Neo Project.
//
// TryParseRpcUriTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using System;
using Xunit;
using static Neo.BlockchainToolkit.Constants;
using BTUtility = Neo.BlockchainToolkit.Utility;

namespace test.bctklib
{
    public class TryParseRpcUriTests
    {
        [Theory]
        [InlineData("mainnet")]
        [InlineData("MAINNET")]
        public void mainnet_alias_resolves_to_first_mainnet_endpoint(string value)
        {
            BTUtility.TryParseRpcUri(value, out var uri).Should().BeTrue();
            uri.Should().Be(new Uri(MAINNET_RPC_ENDPOINTS[0]));
        }

        [Theory]
        [InlineData("testnet")]
        [InlineData("TestNet")]
        public void testnet_alias_resolves_to_first_testnet_endpoint(string value)
        {
            BTUtility.TryParseRpcUri(value, out var uri).Should().BeTrue();
            uri.Should().Be(new Uri(TESTNET_RPC_ENDPOINTS[0]));
        }

        [Theory]
        [InlineData("http://localhost:10332")]
        [InlineData("https://seed1.neo.org:10332")]
        public void absolute_http_and_https_uris_are_accepted(string value)
        {
            BTUtility.TryParseRpcUri(value, out var uri).Should().BeTrue();
            uri.Should().Be(new Uri(value));
        }

        [Theory]
        [InlineData("ftp://host:1234")]   // non-http(s) scheme
        [InlineData("localhost:10332")]   // not an absolute http(s) uri
        [InlineData("not a uri")]
        [InlineData("")]
        public void non_http_or_relative_values_are_rejected(string value)
        {
            // The method only guarantees a non-null uri when it returns true
            // ([NotNullWhen(true)]), so only the boolean result is asserted here.
            BTUtility.TryParseRpcUri(value, out _).Should().BeFalse();
        }
    }
}
