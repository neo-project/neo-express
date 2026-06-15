// Copyright (C) 2015-2026 The Neo Project.
//
// DevWalletTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using NeoExpress.Models;
using Xunit;

namespace test.workflowvalidation;

public class DevWalletTests
{
    [Fact]
    public void WalletRemainsUnlockedAndRenamable()
    {
        var wallet = new DevWallet(ProtocolSettings.Default, "dev");

        Assert.True(wallet.IsUnlocked);

        wallet.Name = "renamed";

        Assert.Equal("renamed", wallet.Name);
    }
}
