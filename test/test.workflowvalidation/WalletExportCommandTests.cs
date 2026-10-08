// Copyright (C) 2015-2026 The Neo Project.
//
// WalletExportCommandTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.Wallets.NEP6;
using NeoExpress;
using NeoExpress.Commands;
using System.IO.Abstractions;
using Xunit;

namespace test.workflowvalidation;

public class WalletExportCommandTests
{
    [Fact]
    public void failed_forced_export_preserves_the_existing_wallet()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"neo-wallet-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var fileSystem = new FileSystem();
            var chain = ExpressChainManagerFactory.CreateChain(1, null);
            chain.Wallets.Add(new ExpressWallet
            {
                Name = "invalid",
                Accounts = [new ExpressWalletAccount { PrivateKey = "not-a-private-key", IsDefault = true }]
            });
            var chainPath = Path.Combine(directory, "chain.neo-express");
            fileSystem.SaveChain(chain, chainPath);
            var output = Path.Combine(directory, "existing.wallet.json");
            File.WriteAllText(output, "previous wallet bytes");
            var command = new WalletCommand.Export(new ExpressChainManagerFactory(fileSystem), fileSystem)
            {
                Name = "invalid",
                Input = chainPath,
                Output = output,
                Password = "test-password",
                Force = true
            };

            Action action = () => command.Execute();

            action.Should().Throw<Exception>();
            File.ReadAllText(output).Should().Be("previous wallet bytes");
            Directory.EnumerateFiles(directory, "*.tmp").Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void missing_password_preserves_the_existing_wallet_without_prompting()
    {
        var (directory, command, output, _) = CreateValidCommand(string.Empty);
        try
        {
            File.WriteAllText(output, "previous wallet bytes");

            var action = () => command.Execute(true, () => throw new InvalidOperationException("must not prompt"));

            action.Should().Throw<Exception>().WithMessage("*--password*");
            File.ReadAllText(output).Should().Be("previous wallet bytes");
            Directory.EnumerateFiles(directory, "*.tmp").Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void successful_forced_export_replaces_the_wallet_and_cleans_the_temporary_file()
    {
        const string password = "test-password";
        var (directory, command, output, settings) = CreateValidCommand(password);
        try
        {
            File.WriteAllText(output, "previous wallet bytes");

            command.Execute(true, () => throw new InvalidOperationException("must not prompt"));

            var exported = new NEP6Wallet(output, password, settings);
            exported.GetAccounts().Should().NotBeEmpty();
            Directory.EnumerateFiles(directory, "*.tmp").Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void export_without_force_rejects_an_existing_wallet_without_staging()
    {
        var (directory, command, output, _) = CreateValidCommand("test-password", force: false);
        try
        {
            File.WriteAllText(output, "previous wallet bytes");

            var action = () => command.Execute(true, () => throw new InvalidOperationException("must not prompt"));

            action.Should().Throw<Exception>().WithMessage("*force*");
            File.ReadAllText(output).Should().Be("previous wallet bytes");
            Directory.EnumerateFiles(directory, "*.tmp").Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static (string Directory, WalletCommand.Export Command, string Output, Neo.ProtocolSettings Settings) CreateValidCommand(string password, bool force = true)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"neo-wallet-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var fileSystem = new FileSystem();
        var chain = ExpressChainManagerFactory.CreateChain(1, null);
        var manager = new ExpressChainManager(fileSystem, chain);
        var wallet = manager.CreateWallet("valid", string.Empty);
        var chainPath = Path.Combine(directory, "chain.neo-express");
        fileSystem.SaveChain(chain, chainPath);
        var output = Path.Combine(directory, "existing.wallet.json");
        var command = new WalletCommand.Export(new ExpressChainManagerFactory(fileSystem), fileSystem)
        {
            Name = wallet.Name,
            Input = chainPath,
            Output = output,
            Password = password,
            Force = force
        };
        return (directory, command, output, chain.GetProtocolSettings());
    }
}
