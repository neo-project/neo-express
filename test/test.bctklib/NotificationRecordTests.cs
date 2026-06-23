// Copyright (C) 2015-2026 The Neo Project.
//
// NotificationRecordTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit.Plugins;
using Neo.Extensions;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using System.Numerics;
using System.Text;
using Xunit;
using NeoArray = Neo.VM.Types.Array;
using NeoByteString = Neo.VM.Types.ByteString;
using NeoInteger = Neo.VM.Types.Integer;

namespace test.bctklib
{
    public class NotificationRecordTests
    {
        [Fact]
        public void Serialize_round_trips_including_the_state_payload()
        {
            var scriptHash = UInt160.Parse("0x0102030405060708090a0b0c0d0e0f1011121314");
            var state = new NeoArray(new Neo.VM.Types.StackItem[]
            {
                new NeoByteString(Encoding.UTF8.GetBytes("from")),
                new NeoInteger(new BigInteger(42)),
            });
            var record = new NotificationRecord(scriptHash, "Transfer", state, InventoryType.TX, UInt256.Zero);

            var restored = record.ToArray().AsSerializable<NotificationRecord>();

            restored.ScriptHash.Should().Be(scriptHash);
            restored.EventName.Should().Be("Transfer");
            restored.InventoryType.Should().Be(InventoryType.TX);
            restored.InventoryHash.Should().Be(UInt256.Zero);
            restored.State.Count.Should().Be(2);
            restored.State[0].GetString().Should().Be("from");
            restored.State[1].GetInteger().Should().Be(new BigInteger(42));
        }
    }
}
