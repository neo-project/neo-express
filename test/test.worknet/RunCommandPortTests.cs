// Copyright (C) 2015-2026 The Neo Project.
//
// RunCommandPortTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Wallets;
using NeoWorkNet.Commands;
using NeoWorkNet.Models;
using System;
using Xunit;

namespace test.worknet;

public class RunCommandPortTests
{
    static WorknetFile CreateWorknetFile()
    {
        var branchInfo = new BranchInfo(
            Network: 0x746E7535,
            AddressVersion: ProtocolSettings.Default.AddressVersion,
            Index: 1,
            IndexHash: UInt256.Zero,
            RootHash: UInt256.Zero,
            Contracts: Array.Empty<ContractInfo>());

        var wallet = new ToolkitWallet("consensus", branchInfo.ProtocolSettings);
        var account = wallet.CreateAccount();
        account.IsDefault = true;

        return new WorknetFile(new Uri("https://seed1t5.neo.org:20331"), branchInfo, wallet);
    }

    [Fact]
    public void get_protocol_settings_uses_default_tcp_port()
    {
        var settings = RunCommand.GetProtocolSettings(CreateWorknetFile());

        settings.SeedList.Should().ContainSingle()
            .Which.Should().Be($"127.0.0.1:{RunCommand.DEFAULT_TCP_PORT}");
    }

    [Fact]
    public void get_protocol_settings_uses_custom_tcp_port()
    {
        const ushort tcpPort = 40333;

        var settings = RunCommand.GetProtocolSettings(CreateWorknetFile(), tcpPort: tcpPort);

        settings.SeedList.Should().ContainSingle()
            .Which.Should().Be($"127.0.0.1:{tcpPort}");
    }

    [Fact]
    public void get_rpc_server_settings_rejects_zero_port()
    {
        Action act = () => RunCommand.GetRpcServerSettings(CreateWorknetFile(), 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void get_rpc_server_settings_uses_custom_rpc_port()
    {
        const ushort rpcPort = 40332;

        var settings = RunCommand.GetRpcServerSettings(CreateWorknetFile(), rpcPort);

        settings.Port.Should().Be(rpcPort);
    }
}
