// Copyright (C) 2015-2026 The Neo Project.
//
// OfflineNodeDisposeTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.BlockchainToolkit;
using Neo.SmartContract;
using NeoExpress;
using NeoExpress.Node;
using Xunit;

namespace test.workflowvalidation;

// mutates the global ApplicationEngine.Provider, so keep the class isolated
[Collection("OfflineNodeDispose")]
public class OfflineNodeDisposeTests
{
    [Fact]
    public void a_disposed_offline_node_releases_the_store_and_restores_the_engine_provider()
    {
        var chain = ExpressChainManagerFactory.CreateChain(1, null);
        var settings = chain.GetProtocolSettings();
        var nodePath = Path.Combine(Path.GetTempPath(), $"neo-express-offline-node-{Guid.NewGuid():N}");
        Directory.CreateDirectory(nodePath);
        try
        {
            var priorProvider = ApplicationEngine.Provider;

            // enableTrace sets the global engine provider; dispose must restore it
            using (var node = new OfflineNode(settings, new RocksDbExpressStorage(nodePath), chain.ConsensusNodes[0].Wallet, chain, enableTrace: true))
            {
            }
            ApplicationEngine.Provider.Should().BeSameAs(priorProvider);

            // a second node over the same path must construct cleanly: the first dispose
            // released the RocksDB lock, and no global store-provider registration is left
            // behind to collide with
            using (var node = new OfflineNode(settings, new RocksDbExpressStorage(nodePath), chain.ConsensusNodes[0].Wallet, chain, enableTrace: false))
            {
            }
        }
        finally
        {
            if (Directory.Exists(nodePath))
                Directory.Delete(nodePath, true);
        }
    }
}
