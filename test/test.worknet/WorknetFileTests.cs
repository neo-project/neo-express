// Copyright (C) 2015-2026 The Neo Project.
//
// WorknetFileTests.cs file belongs to neo-express project and is free
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
using NeoWorkNet;
using NeoWorkNet.Models;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;
using Xunit;

namespace test.worknet;

public class WorknetFileTests
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
        wallet.CreateAccount().IsDefault = true;
        return new WorknetFile(new Uri("https://seed1t5.neo.org:20331"), branchInfo, wallet);
    }

    [Fact]
    public void update_rpc_port_adds_missing_port_property()
    {
        var filename = Path.Combine(Path.GetTempPath(), $"neo-worknet-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(filename, "{\"consensus-nodes\":[{}]}");
            var fileSystem = new FileSystem();

            fileSystem.UpdateWorknetRpcPort(filename, 40332);

            var json = JObject.Parse(File.ReadAllText(filename));
            json["consensus-nodes"]![0]!["rpc-port"]!.Value<ushort>().Should().Be(40332);
        }
        finally
        {
            File.Delete(filename);
        }
    }

    [Fact]
    public async Task load_worknet_reads_rpc_port_and_falls_back_for_legacy_files()
    {
        var filename = Path.Combine(Path.GetTempPath(), $"neo-worknet-{Guid.NewGuid():N}.json");
        try
        {
            var fileSystem = new FileSystem();
            var worknet = CreateWorknetFile();
            fileSystem.SaveWorknetFile(filename, worknet.Uri, worknet.BranchInfo, (ToolkitWallet)worknet.ConsensusWallet);

            var json = JObject.Parse(File.ReadAllText(filename));
            json["consensus-nodes"]![0]!["rpc-port"] = 40332;
            File.WriteAllText(filename, json.ToString());
            (await fileSystem.LoadWorknetAsync(filename)).RpcPort.Should().Be(40332);

            ((JObject)json["consensus-nodes"]![0]!).Remove("rpc-port");
            File.WriteAllText(filename, json.ToString());
            (await fileSystem.LoadWorknetAsync(filename)).RpcPort.Should().Be(WorknetFile.DefaultRpcPort);
        }
        finally
        {
            File.Delete(filename);
        }
    }
}
