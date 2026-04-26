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
using RpcException = Neo.Network.RPC.RpcException;

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

    [Fact]
    public void GetNotificationPaging_UsesDefaultsAndCapsTake()
    {
        ExpressRpcServerPlugin.GetNotificationPaging(CreateNotificationParams())
            .Should().Be((0, 100));

        ExpressRpcServerPlugin.GetNotificationPaging(CreateNotificationParams(skip: 7, take: 101))
            .Should().Be((7, 100));
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, -1)]
    public void GetNotificationPaging_RejectsNegativeValues(int skip, int take)
    {
        var exception = Assert.Throws<RpcException>(
            () => ExpressRpcServerPlugin.GetNotificationPaging(CreateNotificationParams(skip, take)));

        exception.Message.Should().Contain("Invalid params");
    }

    static JArray CreateNotificationParams(int? skip = null, int? take = null)
    {
        var @params = new JArray { new JArray(), new JArray() };
        if (skip is not null)
            @params.Add(skip.Value);
        if (take is not null)
            @params.Add(take.Value);

        return @params;
    }

    static NotificationRecord CreateNotification(int index)
        => new(
            UInt160.Zero,
            $"event-{index}",
            new NeoArray(),
            InventoryType.TX,
            UInt256.Zero);
}
