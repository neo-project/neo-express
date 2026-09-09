// Copyright (C) 2015-2026 The Neo Project.
//
// TryGetWalletNullTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Xunit;

namespace test.bctklib;

public class TryGetWalletNullTests
{
    [Fact]
    public void TryGetWallet_returns_false_when_wallets_is_null()
    {
        var chain = new ExpressChain { Wallets = null! };

        chain.TryGetWallet("alice", out var wallet).Should().BeFalse();
        wallet.Should().BeNull();
    }
}
