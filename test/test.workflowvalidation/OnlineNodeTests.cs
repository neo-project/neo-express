// Copyright (C) 2015-2026 The Neo Project.
//
// OnlineNodeTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Network.RPC;
using NeoExpress.Node;
using System;
using System.Reflection;
using Xunit;

namespace test.workflowvalidation;

public class OnlineNodeTests
{
    [Fact]
    public void Dispose_disposes_the_owned_rpc_client()
    {
        var chain = new ExpressChain();
        var node = new ExpressConsensusNode { RpcPort = 65001 };
        var onlineNode = new OnlineNode(ProtocolSettings.Default, chain, node);

        onlineNode.Dispose();

        var rpcClient = (RpcClient)typeof(OnlineNode)
            .GetField("rpcClient", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(onlineNode)!;

        // A disposed RpcClient throws ObjectDisposedException on use; an
        // undisposed one would instead attempt a network call.
        var act = () => rpcClient.RpcSendAsync("getversion").GetAwaiter().GetResult();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var chain = new ExpressChain();
        var node = new ExpressConsensusNode { RpcPort = 65002 };
        var onlineNode = new OnlineNode(ProtocolSettings.Default, chain, node);

        var act = () =>
        {
            onlineNode.Dispose();
            onlineNode.Dispose();
        };

        act.Should().NotThrow();
    }
}
