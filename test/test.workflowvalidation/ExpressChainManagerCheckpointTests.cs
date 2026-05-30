// Copyright (C) 2015-2026 The Neo Project.
//
// ExpressChainManagerCheckpointTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Moq;
using Neo.BlockchainToolkit.Models;
using NeoExpress;
using Newtonsoft.Json;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using Xunit;

namespace test.workflowvalidation;

public class ExpressChainManagerCheckpointTests
{
    [Fact]
    public void ResolveCheckpointFileName_allows_relative_subdirectory_paths()
    {
        var fileSystem = CreateFileSystem();

        var checkpointPath = ExpressChainManager.ResolveCheckpointFileName(fileSystem, "checkpoints/init");

        checkpointPath.Should().Be(fileSystem.Path.Combine("/work", "checkpoints", "init.neoxp-checkpoint"));
    }

    [Fact]
    public void RestoreCheckpoint_RecoveryOnMoveFail_RestoresBackup()
    {
        var chain = CreateSingleNodeChain();
        var (checkpointPath, checkpointRoot) = CreateCheckpointArchive(chain);
        var currentDirectory = Path.GetDirectoryName(checkpointPath)!;
        var nodeScriptHash = chain.ConsensusNodes[0].Wallet.DefaultAccount?.ScriptHash
            ?? throw new InvalidOperationException("Expected default account script hash");
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        var nodePath = Path.Combine(homeDir, ".neo-express", "blockchain-nodes", nodeScriptHash);
        var oldNodePath = Path.Combine(appData, "Neo-Express", "blockchain-nodes", nodeScriptHash);

        var fileSystemMock = new Mock<IFileSystem>();
        var fileMock = new Mock<IFile>();
        var directoryMock = new Mock<IDirectory>();
        var pathMock = new Mock<IPath>();

        fileSystemMock.SetupGet(f => f.File).Returns(fileMock.Object);
        fileSystemMock.SetupGet(f => f.Directory).Returns(directoryMock.Object);
        fileSystemMock.SetupGet(f => f.Path).Returns(pathMock.Object);

        pathMock.SetupGet(p => p.DirectorySeparatorChar).Returns(Path.DirectorySeparatorChar);
        pathMock.SetupGet(p => p.AltDirectorySeparatorChar).Returns(Path.AltDirectorySeparatorChar);
        pathMock.Setup(p => p.IsPathFullyQualified(It.IsAny<string>())).Returns((string p) => Path.IsPathFullyQualified(p));
        pathMock.Setup(p => p.Combine(It.IsAny<string>(), It.IsAny<string>())).Returns((string a, string b) => Path.Combine(a, b));
        pathMock.Setup(p => p.Combine(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns((string a, string b, string c, string d) => Path.Combine(a, b, c, d));
        pathMock.Setup(p => p.GetExtension(It.IsAny<string>())).Returns((string p) => Path.GetExtension(p));
        pathMock.Setup(p => p.GetFullPath(It.IsAny<string>())).Returns((string p) => Path.GetFullPath(p));
        pathMock.Setup(p => p.GetTempPath()).Returns(currentDirectory);
        pathMock.Setup(p => p.GetRandomFileName()).Returns("restore-temp");

        fileMock.Setup(f => f.Exists(checkpointPath)).Returns(true);

        directoryMock.Setup(d => d.GetCurrentDirectory()).Returns(currentDirectory);

        var nodeExists = true;
        var backupExists = false;
        string? nodePathBackup = null;
        var moveCalls = new List<(string source, string destination)>();
        var moveFailureInjected = false;

        directoryMock.Setup(d => d.Exists(It.IsAny<string>()))
            .Returns((string path) =>
            {
                if (path == nodePath) return nodeExists;
                if (path == oldNodePath) return false;
                if (nodePathBackup is not null && path == nodePathBackup) return backupExists;
                return false;
            });

        directoryMock.Setup(d => d.Move(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((source, destination) =>
            {
                moveCalls.Add((source, destination));

                if (source == nodePath && destination.StartsWith(nodePath + ".backup-", StringComparison.Ordinal))
                {
                    nodeExists = false;
                    backupExists = true;
                    nodePathBackup = destination;
                    return;
                }

                if (destination == nodePath && source != nodePathBackup && !moveFailureInjected)
                {
                    moveFailureInjected = true;
                    throw new IOException("Simulated move failure");
                }

                if (nodePathBackup is not null && source == nodePathBackup && destination == nodePath)
                {
                    backupExists = false;
                    nodeExists = true;
                    return;
                }
            });

        var manager = new ExpressChainManager(fileSystemMock.Object, chain);

        try
        {
            Action action = () => manager.RestoreCheckpoint(checkpointPath, true);

            action.Should().Throw<IOException>();
            moveCalls.Should().Contain(call => call.source.StartsWith(nodePath + ".backup-", StringComparison.Ordinal) && call.destination == nodePath);
            nodeExists.Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(checkpointRoot))
            {
                Directory.Delete(checkpointRoot, true);
            }
        }
    }

    [Fact]
    public void RestoreCheckpoint_CleanupBackupOnSuccess()
    {
        var chain = CreateSingleNodeChain();
        var (checkpointPath, checkpointRoot) = CreateCheckpointArchive(chain);
        var currentDirectory = Path.GetDirectoryName(checkpointPath)!;
        var nodeScriptHash = chain.ConsensusNodes[0].Wallet.DefaultAccount?.ScriptHash
            ?? throw new InvalidOperationException("Expected default account script hash");
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        var nodePath = Path.Combine(homeDir, ".neo-express", "blockchain-nodes", nodeScriptHash);
        var oldNodePath = Path.Combine(appData, "Neo-Express", "blockchain-nodes", nodeScriptHash);

        var fileSystemMock = new Mock<IFileSystem>();
        var fileMock = new Mock<IFile>();
        var directoryMock = new Mock<IDirectory>();
        var pathMock = new Mock<IPath>();

        fileSystemMock.SetupGet(f => f.File).Returns(fileMock.Object);
        fileSystemMock.SetupGet(f => f.Directory).Returns(directoryMock.Object);
        fileSystemMock.SetupGet(f => f.Path).Returns(pathMock.Object);

        pathMock.SetupGet(p => p.DirectorySeparatorChar).Returns(Path.DirectorySeparatorChar);
        pathMock.SetupGet(p => p.AltDirectorySeparatorChar).Returns(Path.AltDirectorySeparatorChar);
        pathMock.Setup(p => p.IsPathFullyQualified(It.IsAny<string>())).Returns((string p) => Path.IsPathFullyQualified(p));
        pathMock.Setup(p => p.Combine(It.IsAny<string>(), It.IsAny<string>())).Returns((string a, string b) => Path.Combine(a, b));
        pathMock.Setup(p => p.Combine(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns((string a, string b, string c, string d) => Path.Combine(a, b, c, d));
        pathMock.Setup(p => p.GetExtension(It.IsAny<string>())).Returns((string p) => Path.GetExtension(p));
        pathMock.Setup(p => p.GetFullPath(It.IsAny<string>())).Returns((string p) => Path.GetFullPath(p));
        pathMock.Setup(p => p.GetTempPath()).Returns(currentDirectory);
        pathMock.Setup(p => p.GetRandomFileName()).Returns("restore-temp-success");

        fileMock.Setup(f => f.Exists(checkpointPath)).Returns(true);

        directoryMock.Setup(d => d.GetCurrentDirectory()).Returns(currentDirectory);

        var nodeExists = true;
        var backupExists = false;
        string? nodePathBackup = null;

        directoryMock.Setup(d => d.Exists(It.IsAny<string>()))
            .Returns((string path) =>
            {
                if (path == nodePath) return nodeExists;
                if (path == oldNodePath) return false;
                if (nodePathBackup is not null && path == nodePathBackup) return backupExists;
                return false;
            });

        directoryMock.Setup(d => d.Move(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((source, destination) =>
            {
                if (source == nodePath && destination.StartsWith(nodePath + ".backup-", StringComparison.Ordinal))
                {
                    nodeExists = false;
                    backupExists = true;
                    nodePathBackup = destination;
                    return;
                }

                if (destination == nodePath)
                {
                    nodeExists = true;
                    return;
                }
            });

        directoryMock.Setup(d => d.Delete(It.IsAny<string>(), true))
            .Callback<string, bool>((path, recursive) =>
            {
                if (path == nodePathBackup)
                {
                    backupExists = false;
                }
            });

        var manager = new ExpressChainManager(fileSystemMock.Object, chain);

        try
        {
            manager.RestoreCheckpoint(checkpointPath, true);

            directoryMock.Verify(d => d.Delete(It.Is<string>(p => p.StartsWith(nodePath + ".backup-", StringComparison.Ordinal)), true), Times.Once);
            backupExists.Should().BeFalse();
            nodeExists.Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(checkpointRoot))
            {
                Directory.Delete(checkpointRoot, true);
            }
        }
    }

    [Theory]
    [InlineData("../../../../etc/passwd")]
    [InlineData("/tmp/passwd")]
    public void ResolveCheckpointFileName_rejects_paths_outside_current_directory(string path)
    {
        var fileSystem = CreateFileSystem();

        var action = () => ExpressChainManager.ResolveCheckpointFileName(fileSystem, path);

        action.Should().Throw<ArgumentException>()
            .WithMessage("Checkpoint path must stay within the current directory*");
    }

    private static (string checkpointPath, string checkpointRoot) CreateCheckpointArchive(ExpressChain chain)
    {
        var checkpointRoot = Path.Combine(Path.GetTempPath(), $"neo-express-checkpoint-test-{Guid.NewGuid():N}");
        var sourcePath = Path.Combine(checkpointRoot, "source");
        Directory.CreateDirectory(sourcePath);

        var scriptHash = chain.ConsensusNodes[0].Wallet.Accounts.SingleOrDefault(a => !a.IsDefault)?.ScriptHash
            ?? throw new InvalidOperationException("Expected consensus multisig account script hash");

        File.WriteAllText(Path.Combine(sourcePath, "ADDRESS.neo-express"),
            $"{chain.Network}{Environment.NewLine}{chain.AddressVersion}{Environment.NewLine}{scriptHash}{Environment.NewLine}");
        File.WriteAllText(Path.Combine(sourcePath, "CURRENT"), "dummy");

        var checkpointPath = Path.Combine(checkpointRoot, "checkpoint.neoxp-checkpoint");
        ZipFile.CreateFromDirectory(sourcePath, checkpointPath);

        return (checkpointPath, checkpointRoot);
    }

    private static ExpressChain CreateSingleNodeChain()
    {
        var solutionDir = FindSolutionDirectory(Directory.GetCurrentDirectory());
        var chainPath = Path.Combine(solutionDir, "test", "test.bctklib", "_testFiles", "default.neo-express.json");
        var chainJson = File.ReadAllText(chainPath);

        return JsonConvert.DeserializeObject<ExpressChain>(chainJson)
            ?? throw new InvalidOperationException($"Could not deserialize chain from {chainPath}");
    }

    private static string FindSolutionDirectory(string startPath)
    {
        var current = new DirectoryInfo(startPath);
        while (current is not null)
        {
            if (current.GetFiles("neo-express.sln").Any())
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find neo-express.sln");
    }

    private static MockFileSystem CreateFileSystem()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>(), "/work");
        fileSystem.Directory.CreateDirectory("/work");
        fileSystem.Directory.SetCurrentDirectory("/work");
        return fileSystem;
    }
}
