// Copyright (C) 2015-2026 The Neo Project.
//
// PrivateKeyParsingTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.Extensions;
using Neo.Wallets;
using NeoExpress;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace test.workflowvalidation;

public class PrivateKeyParsingTests
{
    [Fact]
    public void CreateWalletAcceptsKPrefixedWifPrivateKey()
    {
        var wif = CreateWifWithPrefix('K', out var privateKey);
        var chain = ExpressChainManagerFactory.CreateChain(1, null);
        var manager = new ExpressChainManager(new MockFileSystem(), chain);
        var wallet = manager.CreateWallet("alice", wif);

        wallet.Accounts.Should().ContainSingle();
        wallet.Accounts[0].PrivateKey.HexToBytes().Should().Equal(privateKey);
    }

    [Fact]
    public void ParsePrivateKeyAcceptsBase64PrivateKey()
    {
        var privateKey = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var text = Convert.ToBase64String(privateKey);
        var parsed = ExpressChainManager.ParsePrivateKey(text);

        parsed.Should().Equal(privateKey);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(33)]
    public void ParsePrivateKeyRejectsWrongLengthBase64PrivateKey(int length)
    {
        var text = Convert.ToBase64String(Enumerable.Repeat((byte)1, length).ToArray());
        var parse = () => ExpressChainManager.ParsePrivateKey(text);

        parse.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData(31)]
    [InlineData(33)]
    public void ParsePrivateKeyRejectsWrongLengthHexPrivateKey(int length)
    {
        var text = Convert.ToHexString(Enumerable.Repeat((byte)1, length).ToArray());
        var parse = () => ExpressChainManager.ParsePrivateKey(text);

        parse.Should().Throw<FormatException>();
    }

    static string CreateWifWithPrefix(char prefix, out byte[] privateKey)
    {
        for (var seed = 1; seed < byte.MaxValue; seed++)
        {
            privateKey = Enumerable.Repeat((byte)seed, 32).ToArray();
            var wif = new KeyPair(privateKey).Export();
            if (wif[0] == prefix)
            {
                return wif;
            }
        }

        throw new InvalidOperationException($"Could not create a WIF key with {prefix} prefix");
    }
}
