// Copyright (C) 2015-2024 The Neo Project.
//
// ToolkitWalletTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.BlockchainToolkit.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace test.bctklib;
using static Utility;

public class ToolkitWalletTests
{
    const string expectedAddress = "NeJ7447Mh3ntyn8mb1mkBgTevgkWoxAhLb";
    readonly static UInt160 expectedScriptHash = Neo.Wallets.Helper.ToScriptHash(expectedAddress, ProtocolSettings.Default.AddressVersion);
    readonly static ReadOnlyMemory<byte> expectedScript = Convert.FromHexString("0c21020e220de97ef404352a5fa761b54790ddf554c059198d35314d3721e8679af7604156e7b327");

    static ToolkitWallet GetWallet(string name)
    {
        using var stream = GetResourceStream("default.neo-express.json");
        using var textReader = new StreamReader(stream);
        using var reader = new JsonTextReader(textReader);
        var json = JObject.Load(reader);
        var _json = json["wallets"]?.First(t => t.Value<string>("name") == name) as JObject;
        Assert.NotNull(_json);
        return ToolkitWallet.Parse(_json, ProtocolSettings.Default);
    }

    [Theory]
    [InlineData("devhawk", true)]
    [InlineData("mintest", false)]
    [InlineData("mintest2", true)]
    public void ParseWallet(string walletName, bool isDefault)
    {
        var wallet = GetWallet(walletName);

        Assert.Equal(walletName, wallet.Name);
        Assert.Single(wallet.GetAccounts());
        Assert.NotNull(wallet.GetDefaultAccount());

        var account = wallet.GetDefaultAccount();
        Assert.Equal(expectedAddress, account.Address);
        Assert.Equal(expectedScriptHash, account.ScriptHash);
        Assert.Equal(isDefault, account.IsDefault);
        Assert.False(account.Lock);
        Assert.Null(account.Label);
        Assert.NotNull(account.Contract);
        var contract = account.Contract;
        Assert.True(expectedScript.Span.SequenceEqual(contract.Script));
    }

    // [Fact]
    // public void CanWriteTo()
    // {
    //     var wallet = new ToolkitWallet("test", ProtocolSettings.Default);
    //     wallet.CreateAccount(Convert.FromHexString("2a79cfa210832eb7139c36168e415dc78e2649ea7735060bf1dae04c05050a98"));

    //     var buffer = new ArrayBufferWriter<byte>();
    //     using (var writer = new Utf8JsonWriter(buffer))
    //     {
    //         wallet.WriteTo(writer);
    //         writer.Flush();
    //     }
    //     System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    // }
}
