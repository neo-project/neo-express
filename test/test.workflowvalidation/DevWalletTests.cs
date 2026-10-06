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
using Neo.BlockchainToolkit.Models;
using Neo.Extensions;
using Neo.SmartContract;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using NeoExpress;
using NeoExpress.Models;
using System.IO;
using Xunit;

namespace test.workflowvalidation;

public class DevWalletTests
{
    const string PrivateKey = "2a79cfa210832eb7139c36168e415dc78e2649ea7735060bf1dae04c05050a98";

    [Fact]
    public void WalletRemainsUnlockedAndRenamable()
    {
        var wallet = new DevWallet(ProtocolSettings.Default, "dev");

        Assert.True(wallet.IsUnlocked);

        wallet.Name = "renamed";

        Assert.Equal("renamed", wallet.Name);
    }

    [Fact]
    public void PrivateKeyOnlyAccountDerivesItsContractAndScriptHash()
    {
        var account = DevWalletAccount.FromExpressWalletAccount(
            ProtocolSettings.Default,
            new ExpressWalletAccount { PrivateKey = PrivateKey });

        var keyPair = new KeyPair(Convert.FromHexString(PrivateKey));
        var contract = Contract.CreateSignatureContract(keyPair.PublicKey);

        Assert.Equal(contract.ScriptHash, account.ScriptHash);
        Assert.Equal(contract.Script, account.Contract!.Script);
        Assert.Equal(keyPair.PrivateKey, account.GetKey()!.PrivateKey);
    }

    [Fact]
    public void CustomContractWithoutScriptHashUsesContractScript()
    {
        var keyPair = new KeyPair(Convert.FromHexString(PrivateKey));
        var contract = Contract.CreateMultiSigContract(1, new[] { keyPair.PublicKey });
        var source = new ExpressWalletAccount
        {
            Contract = new ExpressWalletAccount.AccountContract
            {
                Script = contract.Script.ToHexString()
            }
        };

        Assert.Equal(contract.ScriptHash, source.GetScriptHash(ProtocolSettings.Default.AddressVersion));
    }

    [Fact]
    public void KeylessAccountRoundTripsWithoutInventingAContract()
    {
        var scriptHash = UInt160.Parse("0x0101010101010101010101010101010101010101");
        var source = new ExpressWalletAccount
        {
            ScriptHash = scriptHash.ToAddress(ProtocolSettings.Default.AddressVersion),
            Label = "watch-only"
        };

        var account = DevWalletAccount.FromExpressWalletAccount(ProtocolSettings.Default, source);
        var roundTrip = account.ToExpressWalletAccount();

        Assert.False(account.HasKey);
        Assert.Null(account.Contract);
        Assert.Equal(source.ScriptHash, roundTrip.ScriptHash);
        Assert.Equal(source.Label, roundTrip.Label);
        Assert.Null(roundTrip.Contract);
        Assert.Empty(roundTrip.PrivateKey);
    }

    [Fact]
    public void WatchOnlyAccountCanBeExportedToNep6()
    {
        var settings = ProtocolSettings.Default;
        var scriptHash = UInt160.Parse("0x0101010101010101010101010101010101010101");
        var account = DevWalletAccount.FromExpressWalletAccount(settings, new ExpressWalletAccount
        {
            ScriptHash = scriptHash.ToAddress(settings.AddressVersion)
        });
        var wallet = new DevWallet(settings, "watch-only", account);
        var filename = Path.Combine(Path.GetTempPath(), $"neo-express-{Guid.NewGuid():N}.wallet.json");

        try
        {
            wallet.Export(filename, "test-password");
            var exported = new NEP6Wallet(filename, "test-password", settings).GetAccount(scriptHash);
            Assert.NotNull(exported);
            Assert.False(exported!.HasKey);
            Assert.Equal(scriptHash, exported.ScriptHash);
        }
        finally
        {
            File.Delete(filename);
        }
    }

    [Fact]
    public void AccountNameResolutionSupportsLegacyAndKeylessAccounts()
    {
        var scriptHash = UInt160.Parse("0x0101010101010101010101010101010101010101");
        var chain = new ExpressChain
        {
            Wallets =
            [
                new ExpressWallet
                {
                    Name = "legacy",
                    Accounts = [new ExpressWalletAccount { PrivateKey = PrivateKey, IsDefault = true }]
                },
                new ExpressWallet
                {
                    Name = "watch-only",
                    Accounts =
                    [
                        new ExpressWalletAccount
                        {
                            ScriptHash = scriptHash.ToAddress(ProtocolSettings.Default.AddressVersion),
                            IsDefault = true
                        }
                    ]
                }
            ]
        };

        Assert.True(chain.TryGetAccountHash("legacy", out var legacyHash));
        Assert.True(chain.TryGetAccountHash("watch-only", out var watchOnlyHash));
        var expectedLegacyHash = Contract.CreateSignatureContract(
            new KeyPair(Convert.FromHexString(PrivateKey)).PublicKey).ScriptHash;
        Assert.Equal(expectedLegacyHash, legacyHash);
        Assert.Equal(scriptHash, watchOnlyHash);
    }

    [Fact]
    public void AccountNameResolutionFallsBackWhenNoAccountIsDefault()
    {
        var settings = ProtocolSettings.Default;
        var chain = new ExpressChain
        {
            Wallets =
            [
                new ExpressWallet
                {
                    Name = "legacy",
                    Accounts = [new ExpressWalletAccount { PrivateKey = PrivateKey }]
                }
            ]
        };

        var expected = Contract.CreateSignatureContract(
            new KeyPair(Convert.FromHexString(PrivateKey)).PublicKey).ScriptHash;
        Assert.True(chain.TryGetAccountHash("legacy", out var actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ScriptHashUsesTheConfiguredAddressVersion()
    {
        var settings = ProtocolSettings.Default with { AddressVersion = 0x17 };
        var scriptHash = UInt160.Parse("0x0101010101010101010101010101010101010101");
        var account = new ExpressWalletAccount { ScriptHash = scriptHash.ToAddress(settings.AddressVersion) };

        Assert.Equal(scriptHash, account.GetScriptHash(settings.AddressVersion));
    }
}
