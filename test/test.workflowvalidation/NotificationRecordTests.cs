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
using Neo.Extensions;
using Neo.Network.P2P.Payloads;
using NeoExpress.Models;
using System.Linq;
using Xunit;
using ByteString = Neo.VM.Types.ByteString;
using Integer = Neo.VM.Types.Integer;
using NeoArray = Neo.VM.Types.Array;

namespace test.workflowvalidation;

public class NotificationRecordTests
{
    [Fact]
    public void Serialize_round_trips_state()
    {
        var state = new NeoArray(new Neo.VM.Types.StackItem[]
        {
            new ByteString(new byte[20]),
            new ByteString(Enumerable.Repeat((byte)1, 20).ToArray()),
            new Integer(100),
        });
        var record = new NotificationRecord(UInt160.Zero, "Transfer", state, (InventoryType)0, UInt256.Zero);

        var round = record.ToArray().AsSerializable<NotificationRecord>();

        round.EventName.Should().Be("Transfer");
        round.State.Count.Should().Be(3);
        round.State[2].GetInteger().Should().Be(100);
    }
}
