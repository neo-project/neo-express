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
    public void Commit_without_force_must_preserve_existing_config()
    {
        var root = NewRoot();
        try
        {
            var fs = new FileSystem();
            var file = Path.Combine(root, "test.neo-worknet");
            var data = file + ".data";
            var stagedFile = file + ".tmp";
            var stagedData = data + ".tmp";
            File.WriteAllText(file, "original");
            File.WriteAllText(stagedFile, "replacement");
            Directory.CreateDirectory(stagedData);

            Action act = () => WorknetStorageCommit.Commit(fs, false, file, data, stagedFile, stagedData, file + ".bak", data + ".bak");

            act.Should().Throw<Exception>();
            File.ReadAllText(file).Should().Be("original");
            File.Exists(file + ".bak").Should().BeFalse();
            Directory.Exists(data).Should().BeFalse();
            Directory.Exists(stagedData).Should().BeTrue();
        }
        finally { Directory.Delete(root, true); }
    }

    static bool IsCaseSensitive(string dir)
    {
        var probe = Path.Combine(dir, "case-probe");
        File.WriteAllText(probe, "x");
        return !File.Exists(Path.Combine(dir, "CASE-PROBE"));
    }

    [Fact]
    public void Case_variants_resolve_according_to_the_actual_filesystem()
    {
        var root = NewRoot();
        try
        {
            var fs = new FileSystem();
            var pascal = Path.Combine(root, "TestNet.neo-worknet");
            var lower = Path.Combine(root, "testnet.neo-worknet");
            File.WriteAllText(pascal, "x");

            if (IsCaseSensitive(root))
            {
                fs.GetWorknetDataDirectory(pascal).Should().Be(pascal + ".data");
                fs.GetWorknetDataDirectory(lower).Should().Be(lower + ".data");
                fs.GetWorknetDataDirectory(pascal).Should().NotBe(fs.GetWorknetDataDirectory(lower));

                File.WriteAllText(lower, "y");
                fs.GetWorknetDataDirectory(pascal).Should().NotBe(fs.GetWorknetDataDirectory(lower));
            }
            else
            {
                fs.GetWorknetDataDirectory(lower).Should().Be(pascal + ".data");
                fs.GetWorknetDataDirectory(Path.Combine(root, "TESTNET.neo-worknet")).Should().Be(pascal + ".data");
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Default_and_differently_cased_default_follow_the_actual_filesystem()
    {
        var root = NewRoot();
        try
        {
            var fs = new FileSystem();
            File.WriteAllText(Path.Combine(root, "default.neo-worknet"), "x");
            var variant = Path.Combine(root, "Default.neo-worknet");

            if (IsCaseSensitive(root))
                fs.GetWorknetDataDirectory(variant).Should().Be(variant + ".data");
            else
                fs.GetWorknetDataDirectory(variant).Should().Be(Path.Combine(root, "data"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Files_that_do_not_exist_yet_resolve_as_typed()
    {
        var root = NewRoot();
        try
        {
            var fs = new FileSystem();
            var pascal = Path.Combine(root, "TestNet.neo-worknet");
            var lower = Path.Combine(root, "testnet.neo-worknet");
            fs.GetWorknetDataDirectory(pascal).Should().Be(pascal + ".data");
            fs.GetWorknetDataDirectory(lower).Should().Be(lower + ".data");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Relative_and_absolute_spellings_resolve_to_the_same_directory()
    {
        var root = NewRoot();
        try
        {
            var fs = new FileSystem();
            var absolute = Path.Combine(root, "testnet.neo-worknet");
            File.WriteAllText(absolute, "x");
            var relative = Path.GetRelativePath(Environment.CurrentDirectory, absolute);
            fs.GetWorknetDataDirectory(relative).Should().Be(fs.GetWorknetDataDirectory(absolute));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Commit_without_force_preserves_whatever_already_exists(bool hasConfig, bool hasData)
    {
        var root = NewRoot();
        try
        {
            var fs = new FileSystem();
            var file = Path.Combine(root, "t.neo-worknet");
            var data = file + ".data";
            if (hasConfig)
                File.WriteAllText(file, "original");
            if (hasData)
            {
                Directory.CreateDirectory(data);
                File.WriteAllText(Path.Combine(data, "v"), "original");
            }
            File.WriteAllText(file + ".tmp", "new");
            Directory.CreateDirectory(data + ".tmp");

            var act = () => WorknetStorageCommit.Commit(fs, false, file, data, file + ".tmp", data + ".tmp", file + ".bak", data + ".bak");

            act.Should().Throw<IOException>();
            File.Exists(file).Should().Be(hasConfig);
            if (hasConfig)
                File.ReadAllText(file).Should().Be("original");
            Directory.Exists(data).Should().Be(hasData);
            if (hasData)
                File.ReadAllText(Path.Combine(data, "v")).Should().Be("original");
            File.Exists(file + ".bak").Should().BeFalse();
            Directory.Exists(data + ".bak").Should().BeFalse();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Commit_without_force_installs_new_worknet_when_nothing_exists()
    {
        var root = NewRoot();
        try
        {
            var fs = new FileSystem();
            var file = Path.Combine(root, "t.neo-worknet");
            var data = file + ".data";
            File.WriteAllText(file + ".tmp", "new");
            Directory.CreateDirectory(data + ".tmp");

            WorknetStorageCommit.Commit(fs, false, file, data, file + ".tmp", data + ".tmp", file + ".bak", data + ".bak");

            File.ReadAllText(file).Should().Be("new");
            Directory.Exists(data).Should().BeTrue();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Commit_failure_for_a_new_worknet_does_not_leave_installed_data()
    {
        var root = NewRoot();
        try
        {
            var fs = new FileSystem();
            var file = Path.Combine(root, "t.neo-worknet");
            var data = file + ".data";
            Directory.CreateDirectory(data + ".tmp");

            var act = () => WorknetStorageCommit.Commit(fs, false, file, data, file + ".missing", data + ".tmp", file + ".bak", data + ".bak");

            act.Should().Throw<Exception>();
            Directory.Exists(data).Should().BeFalse();
            File.Exists(file).Should().BeFalse();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Commit_with_force_and_only_config_replaces_config()
    {
        var root = NewRoot();
        try
        {
            var fs = new FileSystem();
            var file = Path.Combine(root, "t.neo-worknet");
            var data = file + ".data";
            File.WriteAllText(file, "old");
            File.WriteAllText(file + ".tmp", "new");
            Directory.CreateDirectory(data + ".tmp");

            WorknetStorageCommit.Commit(fs, true, file, data, file + ".tmp", data + ".tmp", file + ".bak", data + ".bak");

            File.ReadAllText(file).Should().Be("new");
            File.ReadAllText(file + ".bak").Should().Be("old");
        }
        finally { Directory.Delete(root, true); }
    }

    const string Id = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void Interrupted_replacement_with_data_moved_aside_is_reported_with_recovery_paths()
    {
        var root = NewRoot();
        try
        {
            var fs = new FileSystem();
            var file = Path.Combine(root, "a.neo-worknet");
            File.WriteAllText(file, "cfg");
            var backup = $"{file}.data.{Id}.bak";
            Directory.CreateDirectory(backup);
            File.WriteAllText(Path.Combine(backup, "v"), "old");

            var act = () => fs.GetExistingWorknetDataDirectory(file);

            act.Should().Throw<Exception>().Where(e => e.Message.Contains(backup) && e.Message.Contains("interrupted"));
            File.ReadAllText(Path.Combine(backup, "v")).Should().Be("old");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Leftover_backups_are_detected_in_every_interrupted_state_and_never_modified()
    {
        var root = NewRoot();
        try
        {
            var fs = new FileSystem();
            var file = Path.Combine(root, "a.neo-worknet");
            var data = file + ".data";
            var backupFile = $"{file}.{Id}.bak";
            var backupData = $"{data}.{Id}.bak";
            File.WriteAllText(backupFile, "old-cfg");
            Directory.CreateDirectory(backupData);
            File.WriteAllText(Path.Combine(backupData, "v"), "old");
            // unrelated items must be ignored
            File.WriteAllText(Path.Combine(root, $"b.neo-worknet.{Id}.bak"), "other");
            File.WriteAllText($"{file}.notanid.bak", "x");

            WorknetStorageCommit.FindLeftoverBackups(fs, file, data)
                .Should().BeEquivalentTo(new[] { backupFile, backupData });

            // new data installed but no config installed
            Directory.CreateDirectory(data);
            WorknetStorageCommit.FindLeftoverBackups(fs, file, data).Should().HaveCount(2);

            File.ReadAllText(backupFile).Should().Be("old-cfg");
            File.ReadAllText(Path.Combine(backupData, "v")).Should().Be("old");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Legacy_default_data_is_accepted_when_it_is_the_only_worknet()
    {
        var root = NewRoot();
        try
        {
            var fs = new FileSystem();
            var file = Path.Combine(root, "default.neo-worknet");
            File.WriteAllText(file, "x");
            Directory.CreateDirectory(Path.Combine(root, "data"));
            fs.GetExistingWorknetDataDirectory(file).Should().Be(Path.Combine(root, "data"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Legacy_data_shared_by_default_and_named_worknet_is_ambiguous_unless_a_marker_resolves_it()
    {
        var root = NewRoot();
        try
        {
            var fs = new FileSystem();
            var def = Path.Combine(root, "default.neo-worknet");
            var data = Path.Combine(root, "data");
            File.WriteAllText(def, "x");
            File.WriteAllText(Path.Combine(root, "testnet.neo-worknet"), "x");
            Directory.CreateDirectory(data);

            ((Action)(() => fs.GetExistingWorknetDataDirectory(def)))
                .Should().Throw<Exception>().WithMessage("*may belong to another worknet*");

            File.WriteAllText(Path.Combine(data, ".worknet-owner"), "default.neo-worknet");
            fs.GetExistingWorknetDataDirectory(def).Should().Be(data);

            File.WriteAllText(Path.Combine(data, ".worknet-owner"), "testnet.neo-worknet");
            ((Action)(() => fs.GetExistingWorknetDataDirectory(def)))
                .Should().Throw<Exception>().WithMessage("*belongs to testnet.neo-worknet*");
            ((Action)(() => fs.EnsureLegacyDataNotShared(def, data)))
                .Should().Throw<Exception>();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Default_with_legacy_data_and_named_worknet_with_own_data_are_both_usable()
    {
        var root = NewRoot();
        try
        {
            var fs = new FileSystem();
            var def = Path.Combine(root, "default.neo-worknet");
            var named = Path.Combine(root, "testnet.neo-worknet");
            File.WriteAllText(def, "x");
            File.WriteAllText(named, "x");
            Directory.CreateDirectory(Path.Combine(root, "data"));
            Directory.CreateDirectory(named + ".data");

            fs.GetExistingWorknetDataDirectory(def).Should().Be(Path.Combine(root, "data"));
            fs.GetExistingWorknetDataDirectory(named).Should().Be(named + ".data");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Owner_marker_records_the_worknet_file_name()
    {
        var root = NewRoot();
        try
        {
            var fs = new FileSystem();
            var data = Path.Combine(root, "data");
            Directory.CreateDirectory(data);
            fs.WriteWorknetOwnerMarker(data, Path.Combine(root, "default.neo-worknet"));
            File.ReadAllText(Path.Combine(data, ".worknet-owner")).Should().Be("default.neo-worknet");
            File.WriteAllText(Path.Combine(root, "other.neo-worknet"), "x");
            fs.GetExistingWorknetDataDirectory(Path.Combine(root, "default.neo-worknet")).Should().Be(data);
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
