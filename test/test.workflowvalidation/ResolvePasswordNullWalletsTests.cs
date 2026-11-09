// Copyright (C) 2015-2026 The Neo Project.
//
// ResolvePasswordNullWalletsTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.BlockchainToolkit.Models;
using NeoExpress;
using Xunit;

namespace test.workflowvalidation;

public class ResolvePasswordNullWalletsTests
{
    [Fact]
    public void ResolvePassword_does_not_throw_when_wallets_is_null()
    {
        var chain = new ExpressChain { Wallets = null! };

        chain.ResolvePassword("unknown", "secret").Should().Be("secret");
    }
}
