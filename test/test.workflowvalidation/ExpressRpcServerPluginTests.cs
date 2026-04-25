// Copyright (C) 2015-2026 The Neo Project.
//
// ExpressRpcServerPluginTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using NeoExpress.Models;
using NeoExpress.Node;
using Xunit;

using NeoArray = Neo.VM.Types.Array;

namespace test.workflowvalidation;

public class ExpressRpcServerPluginTests
{
    [Theory]
    [InlineData(3, 0, 0, true)]
    [InlineData(3, 2, 2, true)]
    [InlineData(3, 3, 3, false)]
    [InlineData(0, 0, 0, false)]
    public void CreateNotificationsResponse_RespectsTakeLimit(
        int sourceCount,
        int take,
        int expectedCount,
        bool expectedTruncated)
    {
        var notifications = Enumerable.Range(0, sourceCount)
            .Select(i => ((uint)i, CreateNotification(i)));

        var response = ExpressRpcServerPlugin.CreateNotificationsResponse(notifications, take);

        response["truncated"]!.AsBoolean().Should().Be(expectedTruncated);
        ((JArray)response["notifications"]!).Count.Should().Be(expectedCount);
    }

    static NotificationRecord CreateNotification(int index)
        => new(
            UInt160.Zero,
            $"event-{index}",
            new NeoArray(),
            InventoryType.TX,
            UInt256.Zero);
}
