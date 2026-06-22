// Copyright (C) 2015-2026 The Neo Project.
//
// ToolkitWalletAccountLockTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using Xunit;

namespace test.bctklib
{
    public class ToolkitWalletAccountLockTests
    {
        [Fact]
        public void Account_round_trips_lock()
        {
            var wallet = new ToolkitWallet("test", ProtocolSettings.Default);
            var privateKey = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
            var account = (ToolkitWallet.Account)wallet.CreateAccount(privateKey);
            account.Lock = true;

            var sw = new StringWriter();
            using (var jw = new JsonTextWriter(sw))
            {
                account.WriteJson(jw);
            }
            var parsed = ToolkitWallet.Account.Parse(JObject.Parse(sw.ToString()), ProtocolSettings.Default);

            parsed.Lock.Should().BeTrue();
        }
    }
}
