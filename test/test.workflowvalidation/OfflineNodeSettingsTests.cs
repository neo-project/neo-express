// Copyright (C) 2015-2026 The Neo Project.
//
// OfflineNodeSettingsTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.BlockchainToolkit;
using NeoExpress;
using NeoExpress.Node;
using Xunit;

namespace test.workflowvalidation;

// mutates global Neo plugin state, so keep it isolated with the other OfflineNode tests
[Collection("OfflineNodeDispose")]
public class OfflineNodeSettingsTests
{
    [Fact]
    public async Task offline_node_applies_initial_policy_settings()
    {
        var chain = ExpressChainManagerFactory.CreateChain(1, null);
        chain.Settings["policy.MinimumDeploymentFee"] = "2000000000";
        chain.Settings["policy.NetworkFeePerByte"] = "2000";
        chain.Settings["policy.StorageFeeFactor"] = "200000";
        chain.Settings["policy.ExecutionFeeFactor"] = "40";
        var settings = chain.GetProtocolSettings();
        var nodePath = Path.Combine(Path.GetTempPath(), $"neo-express-offline-node-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(nodePath);
        try
        {
            using var node = new OfflineNode(
                settings,
                new RocksDbExpressStorage(nodePath),
                chain.ConsensusNodes[0].Wallet,
                chain,
                enableTrace: false);

            var policy = await node.GetPolicyAsync();

            policy.MinimumDeploymentFee.Value.Should().Be(2000000000);
            policy.NetworkFeePerByte.Value.Should().Be(2000);
            policy.StorageFeeFactor.Should().Be(200000);
            policy.ExecutionFeeFactor.Should().Be(40);
        }
        finally
        {
            if (Directory.Exists(nodePath))
                Directory.Delete(nodePath, true);
        }
    }
}
