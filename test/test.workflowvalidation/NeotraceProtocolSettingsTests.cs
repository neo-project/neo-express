// Copyright (C) 2015-2026 The Neo Project.
//
// NeotraceProtocolSettingsTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.Network.RPC.Models;
using Xunit;

namespace test.workflowvalidation;

public class NeotraceProtocolSettingsTests
{
    [Fact]
    public void CreateProtocolSettings_PreservesHardforkHeights()
    {
        var version = new RpcVersion
        {
            Protocol = new RpcVersion.RpcProtocol
            {
                AddressVersion = ProtocolSettings.Default.AddressVersion,
                Hardforks = new Dictionary<Hardfork, uint>
                {
                    [Hardfork.HF_Echidna] = 100,
                    [Hardfork.HF_Gorgon] = 200
                },
                InitialGasDistribution = ProtocolSettings.Default.InitialGasDistribution,
                MaxTraceableBlocks = ProtocolSettings.Default.MaxTraceableBlocks,
                MaxTransactionsPerBlock = ProtocolSettings.Default.MaxTransactionsPerBlock,
                MemoryPoolMaxTransactions = ProtocolSettings.Default.MemoryPoolMaxTransactions,
                MillisecondsPerBlock = ProtocolSettings.Default.MillisecondsPerBlock,
                Network = ProtocolSettings.Default.Network,
                StandbyCommittee = ProtocolSettings.Default.StandbyCommittee,
                ValidatorsCount = ProtocolSettings.Default.ValidatorsCount
            }
        };

        var settings = NeoTrace.Program.CreateProtocolSettings(version);

        settings.Hardforks[Hardfork.HF_Echidna].Should().Be(100);
        settings.Hardforks[Hardfork.HF_Gorgon].Should().Be(200);
    }
}
