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

    [Fact]
    public void Different_worknets_must_use_different_data_directories()
    {
        var fs = new FileSystem();
        var directory = Path.Combine(Path.GetTempPath(), "multiple-worknets");

        var testnet = fs.GetWorknetDataDirectory(Path.Combine(directory, "testnet.neo-worknet"));
        var mainnet = fs.GetWorknetDataDirectory(Path.Combine(directory, "mainnet.neo-worknet"));

        testnet.Should().NotBe(mainnet);
    }

    [Fact]
    public void Default_worknet_keeps_legacy_data_directory()
    {
        var fs = new FileSystem();
        var directory = Path.Combine(Path.GetTempPath(), "multiple-worknets");
        fs.GetWorknetDataDirectory(Path.Combine(directory, "default.neo-worknet"))
            .Should().Be(Path.Combine(directory, "data"));
    }

    [Fact]
    public void Data_directory_resolution_is_deterministic_and_handles_spaces_and_relative_paths()
    {
        var fs = new FileSystem();
        var relative = Path.Combine("some dir", "my net.neo-worknet");
        var first = fs.GetWorknetDataDirectory(relative);
        first.Should().Be(fs.GetWorknetDataDirectory(relative));
        first.Should().Be(fs.GetWorknetDataDirectory(Path.GetFullPath(relative)));
        Path.GetFileName(first).Should().Be("my net.neo-worknet.data");
    }

    [Fact]
    public void Worknets_in_different_directories_use_different_data_directories()
    {
        var fs = new FileSystem();
        var root = Path.Combine(Path.GetTempPath(), "wn-dirs");
        fs.GetWorknetDataDirectory(Path.Combine(root, "a", "x.neo-worknet"))
            .Should().NotBe(fs.GetWorknetDataDirectory(Path.Combine(root, "b", "x.neo-worknet")));
    }

    [Fact]
    public void Named_worknet_does_not_adopt_legacy_data_directory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"wn-legacy-{Guid.NewGuid():N}");
        try
        {
            var fs = new FileSystem();
            Directory.CreateDirectory(Path.Combine(root, "data"));
            File.WriteAllText(Path.Combine(root, "data", "marker"), "legacy");
            var named = Path.Combine(root, "testnet.neo-worknet");

            var act = () => fs.GetExistingWorknetDataDirectory(named);

            act.Should().Throw<Exception>().WithMessage("*legacy shared data directory*");
            Directory.Exists(named + ".data").Should().BeFalse();
            File.ReadAllText(Path.Combine(root, "data", "marker")).Should().Be("legacy");

            fs.GetExistingWorknetDataDirectory(Path.Combine(root, "default.neo-worknet"))
                .Should().Be(Path.Combine(root, "data"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"wn-commit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    [Fact]
    public void Commit_replaces_only_selected_worknet_and_keeps_other_data()
    {
        var root = NewRoot();
        try
        {
            var fs = new FileSystem();
            var a = Path.Combine(root, "a.neo-worknet");
            var b = Path.Combine(root, "b.neo-worknet");
            foreach (var f in new[] { a, b })
            {
                File.WriteAllText(f, "old");
                Directory.CreateDirectory(f + ".data");
                File.WriteAllText(Path.Combine(f + ".data", "v"), "old");
            }
            File.WriteAllText(b + ".new", "new");
            Directory.CreateDirectory(b + ".newdata");
            File.WriteAllText(Path.Combine(b + ".newdata", "v"), "new");

            WorknetStorageCommit.Commit(fs, true, b, b + ".data", b + ".new", b + ".newdata", b + ".bak", b + ".databak");

            File.ReadAllText(b).Should().Be("new");
            File.ReadAllText(Path.Combine(b + ".data", "v")).Should().Be("new");
            File.ReadAllText(a).Should().Be("old");
            File.ReadAllText(Path.Combine(a + ".data", "v")).Should().Be("old");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Commit_without_force_preserves_existing_data()
    {
        var root = NewRoot();
        try
        {
            var fs = new FileSystem();
            var a = Path.Combine(root, "a.neo-worknet");
            Directory.CreateDirectory(a + ".data");
            File.WriteAllText(Path.Combine(a + ".data", "v"), "old");
            Directory.CreateDirectory(a + ".newdata");

            var act = () => WorknetStorageCommit.Commit(fs, false, a, a + ".data", a + ".new", a + ".newdata", a + ".bak", a + ".databak");

            act.Should().Throw<Exception>().WithMessage("*already exists*");
            File.ReadAllText(Path.Combine(a + ".data", "v")).Should().Be("old");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Commit_rolls_back_when_final_file_move_fails()
    {
        var root = NewRoot();
        try
        {
            var fs = new FileSystem();
            var a = Path.Combine(root, "a.neo-worknet");
            File.WriteAllText(a, "old");
            Directory.CreateDirectory(a + ".data");
            File.WriteAllText(Path.Combine(a + ".data", "v"), "old");
            Directory.CreateDirectory(a + ".newdata");
            File.WriteAllText(Path.Combine(a + ".newdata", "v"), "new");

            // the staged config file does not exist, so the last step fails
            var act = () => WorknetStorageCommit.Commit(fs, true, a, a + ".data", a + ".missing", a + ".newdata", a + ".bak", a + ".databak");

            act.Should().Throw<Exception>();
            File.ReadAllText(a).Should().Be("old");
            File.ReadAllText(Path.Combine(a + ".data", "v")).Should().Be("old");
            Directory.Exists(a + ".databak").Should().BeFalse();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Default_force_is_refused_when_legacy_data_may_belong_to_another_worknet()
    {
        var root = NewRoot();
        try
        {
            var fs = new FileSystem();
            Directory.CreateDirectory(Path.Combine(root, "data"));
            File.WriteAllText(Path.Combine(root, "testnet.neo-worknet"), "x");

            var act = () => fs.EnsureLegacyDataNotShared(Path.Combine(root, "default.neo-worknet"), Path.Combine(root, "data"));

            act.Should().Throw<Exception>().WithMessage("*may belong to another worknet*");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Existing_file_casing_is_used_for_data_directory_on_case_insensitive_filesystems()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
            return;
        var root = NewRoot();
        try
        {
            var fs = new FileSystem();
            File.WriteAllText(Path.Combine(root, "TestNet.neo-worknet"), "x");
            fs.GetWorknetDataDirectory(Path.Combine(root, "testnet.neo-worknet"))
                .Should().Be(fs.GetWorknetDataDirectory(Path.Combine(root, "TESTNET.neo-worknet")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Named_worknet_resolves_existing_own_data_directory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"wn-own-{Guid.NewGuid():N}");
        try
        {
            var fs = new FileSystem();
            var named = Path.Combine(root, "testnet.neo-worknet");
            Directory.CreateDirectory(named + ".data");
            fs.GetExistingWorknetDataDirectory(named).Should().Be(named + ".data");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }
}
