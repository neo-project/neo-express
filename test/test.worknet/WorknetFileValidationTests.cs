// Copyright (C) 2015-2026 The Neo Project.
//
// WorknetFileValidationTests.cs file belongs to neo-express project and is free
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

// Regression tests for fuzzing-discovered crash classes in the worknet file
// load path (Utility.LoadWorknetAsync, used by `worknet node/stop/attach`).
// Wrong-typed tokens threw raw InvalidCastException ("Object must implement
// IConvertible") and out-of-range digits threw OverflowException from
// unguarded JToken.Value<T> conversions of the consensus-nodes rpc-port and
// uri properties; both must surface as clean FormatException validation
// errors naming the offending field.
public class WorknetFileValidationTests
{
    [Theory]
    [InlineData("9999999999991")] // digit-bombed port, OverflowException
    [InlineData("{}")]            // wrong-typed token, InvalidCastException
    public async Task invalid_rpc_port_reports_clean_validation_error(string valueJson)
    {
        var filename = Path.Combine(Path.GetTempPath(), $"neo-worknet-{Guid.NewGuid():N}.json");
        try
        {
            var fileSystem = new FileSystem();
            var worknet = CreateWorknetFile();
            fileSystem.SaveWorknetFile(filename, worknet.Uri, worknet.BranchInfo, (ToolkitWallet)worknet.ConsensusWallet);

            var json = JObject.Parse(File.ReadAllText(filename));
            json["consensus-nodes"]![0]!["rpc-port"] = JToken.Parse(valueJson);
            File.WriteAllText(filename, json.ToString());

            var action = async () => await fileSystem.LoadWorknetAsync(filename);

            await action.Should().ThrowAsync<FormatException>().WithMessage("*rpc-port*");
        }
        finally
        {
            File.Delete(filename);
        }
    }

    [Fact]
    public async Task invalid_uri_reports_clean_validation_error()
    {
        var filename = Path.Combine(Path.GetTempPath(), $"neo-worknet-{Guid.NewGuid():N}.json");
        try
        {
            var fileSystem = new FileSystem();
            var worknet = CreateWorknetFile();
            fileSystem.SaveWorknetFile(filename, worknet.Uri, worknet.BranchInfo, (ToolkitWallet)worknet.ConsensusWallet);

            var json = JObject.Parse(File.ReadAllText(filename));
            json["uri"] = new JArray();
            File.WriteAllText(filename, json.ToString());

            var action = async () => await fileSystem.LoadWorknetAsync(filename);

            await action.Should().ThrowAsync<FormatException>().WithMessage("*uri*");
        }
        finally
        {
            File.Delete(filename);
        }
    }

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
}
