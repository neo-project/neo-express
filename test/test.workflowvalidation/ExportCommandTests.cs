// Copyright (C) 2015-2026 The Neo Project.
//
// ExportCommandTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit;
using Neo.Network.P2P.Payloads;
using NeoExpress;
using NeoExpress.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using Xunit;

namespace test.workflowvalidation;

public class ExportCommandTests
{
    static string NewDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"neo-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    static ExportCommand NewCommand() => new(null!, new System.IO.Abstractions.FileSystem());

    static void AssertNoStagingFiles(string directory)
    {
        Directory.EnumerateFiles(directory, "*.tmp").Should().BeEmpty();
        Directory.EnumerateFiles(directory, "*.bak").Should().BeEmpty();
    }

    [Fact]
    public void failed_wallet_export_preserves_existing_files()
    {
        var directory = NewDirectory();
        try
        {
            var chain = ExpressChainManagerFactory.CreateChain(1, null);
            var node = chain.ConsensusNodes[0];
            var wallet = Path.Combine(directory, $"{node.Wallet.Name}.wallet.json");
            var config = Path.Combine(directory, $"{node.Wallet.Name}.config.json");
            File.WriteAllText(wallet, "old wallet");
            File.WriteAllText(config, "old config");
            node.Wallet.Accounts[0].PrivateKey = "not-a-private-key";

            var action = () => NewCommand().ExportNode(chain.GetProtocolSettings(), node, directory, "pw");

            action.Should().Throw<Exception>();
            File.ReadAllText(wallet).Should().Be("old wallet");
            File.ReadAllText(config).Should().Be("old config");
            AssertNoStagingFiles(directory);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void failed_config_replacement_restores_wallet_and_config()
    {
        var directory = NewDirectory();
        try
        {
            var chain = ExpressChainManagerFactory.CreateChain(1, null);
            var node = chain.ConsensusNodes[0];
            var wallet = Path.Combine(directory, $"{node.Wallet.Name}.wallet.json");
            var config = Path.Combine(directory, $"{node.Wallet.Name}.config.json");
            File.WriteAllText(wallet, "old wallet");
            Directory.CreateDirectory(config);
            File.WriteAllText(Path.Combine(config, "marker"), "keep");

            var action = () => NewCommand().ExportNode(chain.GetProtocolSettings(), node, directory, "pw");

            action.Should().Throw<Exception>();
            File.ReadAllText(wallet).Should().Be("old wallet");
            File.ReadAllText(Path.Combine(config, "marker")).Should().Be("keep");
            AssertNoStagingFiles(directory);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void successful_export_replaces_both_files()
    {
        var directory = NewDirectory();
        try
        {
            var chain = ExpressChainManagerFactory.CreateChain(1, null);
            var node = chain.ConsensusNodes[0];
            var settings = chain.GetProtocolSettings();
            var wallet = Path.Combine(directory, $"{node.Wallet.Name}.wallet.json");
            var config = Path.Combine(directory, $"{node.Wallet.Name}.config.json");
            File.WriteAllText(wallet, "old wallet");
            File.WriteAllText(config, "old config");

            NewCommand().ExportNode(settings, node, directory, "pw");

            new Neo.Wallets.NEP6.NEP6Wallet(wallet, "pw", settings).GetAccounts().Should().NotBeEmpty();
            var json = JObject.Parse(File.ReadAllText(config));
            json["ApplicationConfiguration"]!["UnlockWallet"]!["Path"]!.Value<string>().Should().Be(wallet);
            json["ProtocolConfiguration"].Should().NotBeNull();
            AssertNoStagingFiles(directory);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void export_without_existing_files_creates_both_and_failure_leaves_none()
    {
        var directory = NewDirectory();
        try
        {
            var chain = ExpressChainManagerFactory.CreateChain(4, null);
            var settings = chain.GetProtocolSettings();
            var good = chain.ConsensusNodes[0];
            var bad = chain.ConsensusNodes[1];
            bad.Wallet.Accounts[0].PrivateKey = "not-a-private-key";
            var command = NewCommand();

            command.ExportNode(settings, good, directory, "pw");
            var action = () => command.ExportNode(settings, bad, directory, "pw");

            action.Should().Throw<Exception>();
            File.Exists(Path.Combine(directory, $"{good.Wallet.Name}.wallet.json")).Should().BeTrue();
            File.Exists(Path.Combine(directory, $"{good.Wallet.Name}.config.json")).Should().BeTrue();
            File.Exists(Path.Combine(directory, $"{bad.Wallet.Name}.wallet.json")).Should().BeFalse();
            File.Exists(Path.Combine(directory, $"{bad.Wallet.Name}.config.json")).Should().BeFalse();
            AssertNoStagingFiles(directory);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    static ExportCommand NewCommand(Action<Moq.Mock<System.IO.Abstractions.FileWrapper>> configure)
    {
        var real = new System.IO.Abstractions.FileSystem();
        var file = new Moq.Mock<System.IO.Abstractions.FileWrapper>(real) { CallBase = true };
        configure(file);
        var fileSystem = new Moq.Mock<System.IO.Abstractions.IFileSystem>();
        fileSystem.SetupGet(f => f.File).Returns(file.Object);
        fileSystem.SetupGet(f => f.Directory).Returns(real.Directory);
        fileSystem.SetupGet(f => f.Path).Returns(real.Path);
        return new ExportCommand(null!, fileSystem.Object);
    }

    [Fact]
    public void failed_config_write_preserves_existing_files_and_cleans_temporary_files()
    {
        var directory = NewDirectory();
        try
        {
            var chain = ExpressChainManagerFactory.CreateChain(1, null);
            var node = chain.ConsensusNodes[0];
            var wallet = Path.Combine(directory, $"{node.Wallet.Name}.wallet.json");
            var config = Path.Combine(directory, $"{node.Wallet.Name}.config.json");
            File.WriteAllText(wallet, "old wallet");
            File.WriteAllText(config, "old config");
            var command = NewCommand(file => file
                .Setup(f => f.Open(Moq.It.Is<string>(p => p.Contains(".config.json")), FileMode.CreateNew, FileAccess.Write))
                .Throws(new IOException("simulated write failure")));

            var action = () => command.ExportNode(chain.GetProtocolSettings(), node, directory, "pw");

            action.Should().Throw<IOException>().WithMessage("simulated write failure");
            File.ReadAllText(wallet).Should().Be("old wallet");
            File.ReadAllText(config).Should().Be("old config");
            AssertNoStagingFiles(directory);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void failure_during_second_replacement_restores_original_files()
    {
        var directory = NewDirectory();
        try
        {
            var chain = ExpressChainManagerFactory.CreateChain(1, null);
            var node = chain.ConsensusNodes[0];
            var wallet = Path.Combine(directory, $"{node.Wallet.Name}.wallet.json");
            var config = Path.Combine(directory, $"{node.Wallet.Name}.config.json");
            File.WriteAllText(wallet, "old wallet");
            File.WriteAllText(config, "old config");
            var command = NewCommand(file => file
                .Setup(f => f.Move(Moq.It.Is<string>(s => s.EndsWith(".tmp")), config))
                .Throws(new IOException("simulated move failure")));

            var action = () => command.ExportNode(chain.GetProtocolSettings(), node, directory, "pw");

            action.Should().Throw<IOException>().WithMessage("simulated move failure");
            File.ReadAllText(wallet).Should().Be("old wallet");
            File.ReadAllText(config).Should().Be("old config");
            AssertNoStagingFiles(directory);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void failed_rollback_preserves_and_reports_backups()
    {
        var directory = NewDirectory();
        try
        {
            var chain = ExpressChainManagerFactory.CreateChain(1, null);
            var node = chain.ConsensusNodes[0];
            var wallet = Path.Combine(directory, $"{node.Wallet.Name}.wallet.json");
            var config = Path.Combine(directory, $"{node.Wallet.Name}.config.json");
            File.WriteAllText(wallet, "old wallet");
            File.WriteAllText(config, "old config");
            var command = NewCommand(file =>
            {
                file.Setup(f => f.Move(Moq.It.Is<string>(s => s.EndsWith(".tmp")), config))
                    .Throws(new IOException("simulated move failure"));
                file.Setup(f => f.Move(Moq.It.Is<string>(s => s.EndsWith(".bak")), wallet))
                    .Throws(new IOException("simulated rollback failure"));
            });

            var action = () => command.ExportNode(chain.GetProtocolSettings(), node, directory, "pw");

            var ex = action.Should().Throw<IOException>().Which;
            ex.Message.Should().Contain(".bak");
            ex.Message.Should().NotContain("pw");
            ex.InnerException!.Message.Should().Be("simulated move failure");
            var backups = Directory.EnumerateFiles(directory, "*.bak").ToList();
            backups.Should().ContainSingle();
            File.ReadAllText(backups[0]).Should().Be("old wallet");
            ex.Message.Should().Contain(backups[0]);
            File.ReadAllText(config).Should().Be("old config");
            Directory.EnumerateFiles(directory, "*.tmp").Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void multi_node_export_keeps_independent_pairs_and_preserves_outputs_after_failure()
    {
        var directory = NewDirectory();
        try
        {
            var chain = ExpressChainManagerFactory.CreateChain(4, null);
            var settings = chain.GetProtocolSettings();
            foreach (var node in chain.ConsensusNodes)
            {
                File.WriteAllText(Path.Combine(directory, $"{node.Wallet.Name}.wallet.json"), $"old wallet {node.Wallet.Name}");
                File.WriteAllText(Path.Combine(directory, $"{node.Wallet.Name}.config.json"), $"old config {node.Wallet.Name}");
            }
            chain.ConsensusNodes[2].Wallet.Accounts[0].PrivateKey = "not-a-private-key";
            var command = NewCommand();

            var action = () =>
            {
                foreach (var node in chain.ConsensusNodes)
                    command.ExportNode(settings, node, directory, "pw");
            };

            action.Should().Throw<Exception>();
            for (var i = 0; i < 4; i++)
            {
                var name = chain.ConsensusNodes[i].Wallet.Name;
                var walletText = File.ReadAllText(Path.Combine(directory, $"{name}.wallet.json"));
                var configText = File.ReadAllText(Path.Combine(directory, $"{name}.config.json"));
                if (i < 2)
                {
                    walletText.Should().NotStartWith("old");
                    JObject.Parse(configText)["ApplicationConfiguration"]!["UnlockWallet"]!["Path"]!
                        .Value<string>().Should().Be(Path.Combine(directory, $"{name}.wallet.json"));
                }
                else
                {
                    walletText.Should().Be($"old wallet {name}");
                    configText.Should().Be($"old config {name}");
                }
            }
            AssertNoStagingFiles(directory);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void undeletable_backups_are_reported_after_successful_export()
    {
        var directory = NewDirectory();
        try
        {
            var chain = ExpressChainManagerFactory.CreateChain(1, null);
            var node = chain.ConsensusNodes[0];
            var wallet = Path.Combine(directory, $"{node.Wallet.Name}.wallet.json");
            var config = Path.Combine(directory, $"{node.Wallet.Name}.config.json");
            File.WriteAllText(wallet, "old wallet");
            File.WriteAllText(config, "old config");
            var command = NewCommand(file => file
                .Setup(f => f.Delete(Moq.It.Is<string>(p => p.EndsWith(".bak"))))
                .Throws(new IOException("locked")));
            using var warnings = new StringWriter();

            command.ExportNode(chain.GetProtocolSettings(), node, directory, "secret-pw", warnings);

            File.ReadAllText(wallet).Should().NotBe("old wallet");
            var backups = Directory.EnumerateFiles(directory, "*.bak").ToList();
            backups.Should().HaveCount(2);
            foreach (var backup in backups)
                warnings.ToString().Should().Contain(backup);
            warnings.ToString().Should().NotContain("secret-pw");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void failed_removal_of_new_wallet_is_reported_when_no_original_existed()
    {
        var directory = NewDirectory();
        try
        {
            var chain = ExpressChainManagerFactory.CreateChain(1, null);
            var node = chain.ConsensusNodes[0];
            var wallet = Path.Combine(directory, $"{node.Wallet.Name}.wallet.json");
            var config = Path.Combine(directory, $"{node.Wallet.Name}.config.json");
            var command = NewCommand(file =>
            {
                file.Setup(f => f.Move(Moq.It.Is<string>(s => s.EndsWith(".tmp")), config))
                    .Throws(new IOException("simulated move failure"));
                file.Setup(f => f.Delete(wallet))
                    .Throws(new IOException("simulated delete failure"));
            });

            var action = () => command.ExportNode(chain.GetProtocolSettings(), node, directory, "pw");

            var ex = action.Should().Throw<IOException>().Which;
            ex.Message.Should().Contain(wallet);
            ex.InnerException!.Message.Should().Be("simulated move failure");
            File.Exists(wallet).Should().BeTrue();
            File.Exists(config).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void undeletable_temporary_files_are_reported()
    {
        var directory = NewDirectory();
        try
        {
            var chain = ExpressChainManagerFactory.CreateChain(1, null);
            var node = chain.ConsensusNodes[0];
            var config = Path.Combine(directory, $"{node.Wallet.Name}.config.json");
            var command = NewCommand(file =>
            {
                file.Setup(f => f.Move(Moq.It.Is<string>(s => s.EndsWith(".tmp")), config))
                    .Throws(new IOException("simulated move failure"));
                file.Setup(f => f.Delete(Moq.It.Is<string>(p => p.EndsWith(".tmp"))))
                    .Throws(new IOException("locked"));
            });
            using var warnings = new StringWriter();

            var action = () => command.ExportNode(chain.GetProtocolSettings(), node, directory, "secret-pw", warnings);

            action.Should().Throw<IOException>().WithMessage("simulated move failure");
            var leftovers = Directory.EnumerateFiles(directory, "*.tmp").ToList();
            leftovers.Should().ContainSingle();
            warnings.ToString().Should().Contain(leftovers[0]);
            warnings.ToString().Should().NotContain("secret-pw");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void export_writes_resolved_protocol_configuration_for_neo_cli()
    {
        var chain = ExpressChainManagerFactory.CreateChain(1, null);
        chain.Settings["protocol.MillisecondsPerBlock"] = "3000";
        chain.Settings["protocol.MaxTransactionsPerBlock"] = "1024";
        chain.Settings["protocol.MemoryPoolMaxTransactions"] = "60000";
        chain.Settings["protocol.MaxTraceableBlocks"] = "1000";
        chain.Settings["protocol.MaxValidUntilBlockIncrement"] = "100";
        chain.Settings["protocol.InitialGasDistribution"] = "123456789";
        chain.Settings["protocol.Hardforks.HF_Echidna"] = "0";
        chain.Settings["protocol.Hardforks.HF_Faun"] = "42";
        chain.Settings["protocol.Hardforks.HF_Gorgon"] = "42";
        chain.Settings["protocol.Hardforks.HF_Huyao"] = "84";
        var settings = chain.GetProtocolSettings();

        using var textWriter = new StringWriter();
        using (var jsonWriter = new JsonTextWriter(textWriter))
        {
            jsonWriter.WriteStartObject();
            ExportCommand.WriteProtocolConfiguration(jsonWriter, settings);
            jsonWriter.WriteEndObject();
        }

        var json = JObject.Parse(textWriter.ToString());
        var protocol = json["ProtocolConfiguration"]!;
        protocol["Magic"].Should().BeNull();
        protocol["Network"]!.Value<uint>().Should().Be(chain.Network);
        protocol["AddressVersion"]!.Value<byte>().Should().Be(chain.AddressVersion);
        protocol["MillisecondsPerBlock"]!.Value<uint>().Should().Be(3000);
        protocol["MaxTransactionsPerBlock"]!.Value<uint>().Should().Be(1024);
        protocol["MemoryPoolMaxTransactions"]!.Value<int>().Should().Be(60000);
        protocol["MaxTraceableBlocks"]!.Value<uint>().Should().Be(1000);
        protocol["MaxValidUntilBlockIncrement"]!.Value<uint>().Should().Be(100);
        protocol["InitialGasDistribution"]!.Value<ulong>().Should().Be(123456789);
        protocol["ValidatorsCount"]!.Value<int>().Should().Be(1);
        protocol["StandbyCommittee"]!.Should().HaveCount(1);
        protocol["SeedList"]!.Values<string>().Should().ContainSingle().Which.Should().EndWith(":50013");
        protocol["Hardforks"]![nameof(Hardfork.HF_Faun)]!.Value<uint>().Should().Be(42);
        protocol["Hardforks"]![nameof(Hardfork.HF_Gorgon)]!.Value<uint>().Should().Be(42);
        protocol["Hardforks"]![nameof(Hardfork.HF_Huyao)]!.Value<uint>().Should().Be(84);

        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(textWriter.ToString()));
            var loaded = ProtocolSettings.Load(stream);
            loaded.Network.Should().Be(chain.Network);
            loaded.MillisecondsPerBlock.Should().Be(3000);
            loaded.MaxTransactionsPerBlock.Should().Be(1024);
            loaded.MemoryPoolMaxTransactions.Should().Be(60000);
            loaded.MaxTraceableBlocks.Should().Be(1000);
            loaded.MaxValidUntilBlockIncrement.Should().Be(100);
            loaded.InitialGasDistribution.Should().Be(123456789);
            loaded.Hardforks[Hardfork.HF_Faun].Should().Be(42);
            loaded.Hardforks[Hardfork.HF_Gorgon].Should().Be(42);
            loaded.Hardforks[Hardfork.HF_Huyao].Should().Be(84);
        }
        finally
        {
            ProtocolSettings.Custom = null;
        }
    }
}
