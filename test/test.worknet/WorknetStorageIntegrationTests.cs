// Copyright (C) 2015-2026 The Neo Project.
//
// WorknetStorageIntegrationTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.BlockchainToolkit.Persistence;
using NeoWorkNet;
using System;
using System.IO;
using System.IO.Abstractions;
using Xunit;

namespace test.worknet;

// Uses temporary directories and disposable RocksDB databases only; no network access.
public class WorknetStorageIntegrationTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), $"wn-int-{Guid.NewGuid():N}");
    readonly FileSystem fs = new();

    public WorknetStorageIntegrationTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    static void Write(string dir, string value)
    {
        using var db = RocksDbUtility.OpenDb(dir);
        db.Put("key", value);
    }

    static string? Read(string dir)
    {
        using var db = RocksDbUtility.OpenDb(dir);
        return db.Get("key");
    }

    string Create(string name, string value)
    {
        var file = Path.Combine(root, name);
        File.WriteAllText(file, value);
        var dataDir = fs.GetWorknetDataDirectory(file);
        Directory.CreateDirectory(dataDir);
        Write(dataDir, value);
        return file;
    }

    void Replace(string file, string value, bool force, bool failBeforeSwap = false)
    {
        var dataDir = fs.GetWorknetDataDirectory(file);
        var id = Guid.NewGuid().ToString("N");
        var tempFile = $"{file}.{id}.tmp";
        var tempData = $"{dataDir}.{id}.tmp";
        File.WriteAllText(tempFile, value);
        Directory.CreateDirectory(tempData);
        Write(tempData, value);
        if (failBeforeSwap)
            File.Delete(tempFile);
        WorknetStorageCommit.Commit(fs, force, file, dataDir, tempFile, tempData, $"{file}.{id}.bak", $"{dataDir}.{id}.bak");
    }

    [Fact]
    public void ScenarioE_force_on_named_worknet_leaves_default_legacy_data_unchanged()
    {
        var def = Create("default.neo-worknet", "D");
        var named = Create("testnet.neo-worknet", "T");
        File.WriteAllText(Path.Combine(fs.GetWorknetDataDirectory(def), ".worknet-owner"), "default.neo-worknet");

        fs.EnsureLegacyDataNotShared(named, fs.GetWorknetDataDirectory(named));
        Replace(named, "T2", force: true);

        Read(fs.GetWorknetDataDirectory(def)).Should().Be("D");
        Read(fs.GetWorknetDataDirectory(named)).Should().Be("T2");
    }

    [Fact]
    public void Force_on_default_is_refused_when_legacy_data_is_ambiguous_and_data_is_untouched()
    {
        var def = Create("default.neo-worknet", "D");
        File.WriteAllText(Path.Combine(root, "testnet.neo-worknet"), "legacy config without own data");

        var act = () => fs.EnsureLegacyDataNotShared(def, fs.GetWorknetDataDirectory(def));

        act.Should().Throw<Exception>().WithMessage("*may belong to another worknet*");
        Read(fs.GetWorknetDataDirectory(def)).Should().Be("D");
    }

    [Fact]
    public void Force_on_default_is_refused_when_named_worknet_has_own_data_but_legacy_data_is_unmarked()
    {
        // Migration sequence: the unmarked legacy "data" directory holds TestNet's database, then
        // TestNet is recreated with its own ".data" directory. The new directory does not prove the
        // legacy data is not TestNet's earlier database, so forced replacement must stay refused
        // until ownership of the legacy directory is explicitly recorded.
        var def = Path.Combine(root, "default.neo-worknet");
        File.WriteAllText(def, "D");
        Directory.CreateDirectory(Path.Combine(root, "data"));
        Write(Path.Combine(root, "data"), "T-legacy");
        var named = Create("testnet.neo-worknet", "T");

        var act = () => fs.EnsureLegacyDataNotShared(def, fs.GetWorknetDataDirectory(def));

        act.Should().Throw<Exception>().WithMessage("*may belong to another worknet*");
        Read(Path.Combine(root, "data")).Should().Be("T-legacy");

        File.WriteAllText(Path.Combine(root, "data", ".worknet-owner"), "default.neo-worknet");
        fs.EnsureLegacyDataNotShared(def, fs.GetWorknetDataDirectory(def));
        Replace(def, "D2", force: true);

        Read(fs.GetWorknetDataDirectory(def)).Should().Be("D2");
        Read(fs.GetWorknetDataDirectory(named)).Should().Be("T");
    }

    [Fact]
    public void ScenarioA_worknets_in_same_directory_have_isolated_databases()
    {
        var a = Create("a.neo-worknet", "A");
        var b = Create("b.neo-worknet", "B");

        Read(fs.GetWorknetDataDirectory(a)).Should().Be("A");
        Read(fs.GetWorknetDataDirectory(b)).Should().Be("B");
    }

    [Fact]
    public void ScenarioB_force_replaces_only_selected_worknet()
    {
        var a = Create("a.neo-worknet", "A");
        var b = Create("b.neo-worknet", "B");

        Replace(b, "B2", force: true);

        Read(fs.GetWorknetDataDirectory(a)).Should().Be("A");
        Read(fs.GetWorknetDataDirectory(b)).Should().Be("B2");
        File.ReadAllText(b).Should().Be("B2");
    }

    [Fact]
    public void ScenarioC_legacy_default_worknet_data_remains_accessible()
    {
        var file = Path.Combine(root, "default.neo-worknet");
        File.WriteAllText(file, "legacy");
        Directory.CreateDirectory(Path.Combine(root, "data"));
        Write(Path.Combine(root, "data"), "legacy");

        var dataDir = fs.GetExistingWorknetDataDirectory(file);

        Read(dataDir).Should().Be("legacy");
    }

    [Fact]
    public void ScenarioC_legacy_data_is_not_adopted_by_named_worknet_and_is_left_untouched()
    {
        Directory.CreateDirectory(Path.Combine(root, "data"));
        Write(Path.Combine(root, "data"), "legacy");
        var named = Path.Combine(root, "testnet.neo-worknet");
        File.WriteAllText(named, "cfg");

        var act = () => fs.GetExistingWorknetDataDirectory(named);

        act.Should().Throw<Exception>();
        Read(Path.Combine(root, "data")).Should().Be("legacy");
    }

    [Fact]
    public void ScenarioD_failure_during_replacement_keeps_original_usable()
    {
        var a = Create("a.neo-worknet", "A");

        var act = () => Replace(a, "A2", force: true, failBeforeSwap: true);

        act.Should().Throw<Exception>();
        File.ReadAllText(a).Should().Be("A");
        Read(fs.GetWorknetDataDirectory(a)).Should().Be("A");
        Directory.GetDirectories(root, "*.bak").Should().BeEmpty();
    }

    [Fact]
    public void Creation_without_force_preserves_existing_database()
    {
        var a = Create("a.neo-worknet", "A");

        var act = () => Replace(a, "A2", force: false);

        act.Should().Throw<Exception>().WithMessage("*already exists*");
        Read(fs.GetWorknetDataDirectory(a)).Should().Be("A");
    }

    [Fact]
    public void Symlinked_data_directory_is_not_replaced()
    {
        var target = Path.Combine(root, "elsewhere");
        Directory.CreateDirectory(target);
        Write(target, "outside");
        var a = Path.Combine(root, "a.neo-worknet");
        File.WriteAllText(a, "A");
        var dataDir = fs.GetWorknetDataDirectory(a);
        try
        {
            Directory.CreateSymbolicLink(dataDir, target);
        }
        catch (Exception)
        {
            return; // symlink creation not permitted in this environment
        }

        var act = () => Replace(a, "A2", force: true);

        act.Should().Throw<Exception>().WithMessage("*symbolic link*");
        Read(target).Should().Be("outside");
        File.ReadAllText(a).Should().Be("A");
    }
}
