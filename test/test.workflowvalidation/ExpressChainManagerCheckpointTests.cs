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
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Cryptography.ECC;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using NeoExpress;
using NeoExpress.Commands;
using NeoExpress.Models;
using NeoExpress.Node;
using Newtonsoft.Json;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using System.Numerics;
using Xunit;

namespace test.workflowvalidation;

public class ExpressChainManagerCheckpointTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RestoreCheckpoint_preserves_extraction_error_and_node_data(bool cleanupFails)
    {
        var chain = CreateSingleNodeChain();
        var (checkpointPath, checkpointRoot) = CreateCheckpointArchive(chain);
        var restorePath = Path.Combine(checkpointRoot, "restore-temp");
        using (var archive = ZipFile.Open(checkpointPath, ZipArchiveMode.Update))
        {
            archive.CreateEntry("CURRENT");
        }

        var fileSystem = new Mock<IFileSystem>();
        var file = new Mock<IFile>();
        var directory = new Mock<IDirectory>();
        var path = new Mock<IPath>();
        fileSystem.SetupGet(f => f.File).Returns(file.Object);
        fileSystem.SetupGet(f => f.Directory).Returns(directory.Object);
        fileSystem.SetupGet(f => f.Path).Returns(path.Object);
        path.SetupGet(p => p.DirectorySeparatorChar).Returns(Path.DirectorySeparatorChar);
        path.SetupGet(p => p.AltDirectorySeparatorChar).Returns(Path.AltDirectorySeparatorChar);
        path.Setup(p => p.IsPathFullyQualified(It.IsAny<string>())).Returns((string p) => Path.IsPathFullyQualified(p));
        path.Setup(p => p.Combine(It.IsAny<string>(), It.IsAny<string>())).Returns((string a, string b) => Path.Combine(a, b));
        path.Setup(p => p.Combine(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns((string a, string b, string c, string d) => Path.Combine(a, b, c, d));
        path.Setup(p => p.GetExtension(It.IsAny<string>())).Returns((string p) => Path.GetExtension(p));
        path.Setup(p => p.GetFullPath(It.IsAny<string>())).Returns((string p) => Path.GetFullPath(p));
        path.Setup(p => p.GetTempPath()).Returns(checkpointRoot);
        path.Setup(p => p.GetRandomFileName()).Returns("restore-temp");
        file.Setup(f => f.Exists(checkpointPath)).Returns(true);
        directory.Setup(d => d.GetCurrentDirectory()).Returns(checkpointRoot);
        directory.Setup(d => d.Exists(It.IsAny<string>())).Returns((string p) => Directory.Exists(p));
        if (cleanupFails)
        {
            directory.Setup(d => d.Delete(restorePath, true))
                .Throws(new UnauthorizedAccessException("Temporary directory cleanup denied"));
        }
        else
        {
            directory.Setup(d => d.Delete(It.IsAny<string>(), true)).Callback<string, bool>(Directory.Delete);
        }
        var manager = new ExpressChainManager(fileSystem.Object, chain);

        try
        {
            Action action = () => manager.RestoreCheckpoint(checkpointPath, true);

            action.Should().ThrowExactly<IOException>();
            Directory.Exists(restorePath).Should().Be(cleanupFails);
            directory.Verify(d => d.Delete(restorePath, true), Times.Once);
            directory.Verify(d => d.Move(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
        finally
        {
            Directory.Delete(checkpointRoot, true);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OpenCheckpoint_removes_extracted_files_on_failure(bool failDuringExtraction)
    {
        var (checkpointPath, checkpointRoot) = CreateCheckpointArchive(CreateSingleNodeChain());
        var extractedPath = Path.Combine(checkpointRoot, "extracted-checkpoint");
        using (var archive = ZipFile.Open(checkpointPath, ZipArchiveMode.Update))
        {
            if (failDuringExtraction)
            {
                archive.CreateEntry("CURRENT");
            }
        }

        try
        {
            Action action = () =>
            {
                using var storage = CheckpointExpressStorage.OpenCheckpoint(checkpointPath, extractedPath);
            };

            if (failDuringExtraction)
            {
                action.Should().Throw<IOException>();
            }
            else
            {
                action.Should().Throw<RocksDbSharp.RocksDbException>();
            }
            Directory.Exists(extractedPath).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(checkpointRoot, true);
        }
    }

    [Fact]
    public void ResolveCheckpointFileName_allows_relative_subdirectory_paths()
    {
        var fileSystem = CreateFileSystem();

        var checkpointPath = ExpressChainManager.ResolveCheckpointFileName(fileSystem, "checkpoints/init");

        checkpointPath.Should().Be(fileSystem.Path.Combine("/work", "checkpoints", "init.neoxp-checkpoint"));
    }

    [Fact]
    public async Task CreateCheckpointAsync_keeps_existing_checkpoint_when_creation_fails()
    {
        var fileSystem = CreateFileSystem();
        fileSystem.Directory.CreateDirectory("/work/checkpoints");
        fileSystem.File.WriteAllText("/work/checkpoints/init.neoxp-checkpoint", "existing");
        var node = new CheckpointStubNode
        {
            CreateCheckpoint = _ => throw new InvalidOperationException("checkpoint creation failed")
        };
        var manager = new ExpressChainManager(fileSystem, CreateSingleNodeChain());

        var act = () => manager.CreateCheckpointAsync(node, "checkpoints/init", force: true);

        await act.Should().ThrowAsync<InvalidOperationException>();
        fileSystem.File.ReadAllText("/work/checkpoints/init.neoxp-checkpoint").Should().Be("existing");
        fileSystem.Directory.GetFiles("/work/checkpoints").Should().ContainSingle();
    }

    [Fact]
    public async Task CreateCheckpointAsync_replaces_existing_checkpoint_after_success()
    {
        var fileSystem = CreateFileSystem();
        fileSystem.Directory.CreateDirectory("/work/checkpoints");
        fileSystem.File.WriteAllText("/work/checkpoints/init.neoxp-checkpoint", "existing");
        string? createdPath = null;
        var node = new CheckpointStubNode
        {
            CreateCheckpoint = path =>
            {
                createdPath = path;
                fileSystem.File.WriteAllText(path, "replacement");
                return Task.FromResult(IExpressNode.CheckpointMode.Offline);
            }
        };
        var manager = new ExpressChainManager(fileSystem, CreateSingleNodeChain());

        var (path, mode) = await manager.CreateCheckpointAsync(node, "checkpoints/init", force: true);

        path.Should().Be(fileSystem.Path.Combine("/work", "checkpoints", "init.neoxp-checkpoint"));
        mode.Should().Be(IExpressNode.CheckpointMode.Offline);
        createdPath.Should().NotBeNull().And.NotBe(path);
        fileSystem.File.Exists(createdPath!).Should().BeFalse();
        fileSystem.File.ReadAllText(path).Should().Be("replacement");
        fileSystem.Directory.GetFiles("/work/checkpoints").Should().ContainSingle();
    }

    [Fact]
    public async Task CreateCheckpointAsync_rejects_existing_checkpoint_without_force()
    {
        var fileSystem = CreateFileSystem();
        fileSystem.Directory.CreateDirectory("/work/checkpoints");
        fileSystem.File.WriteAllText("/work/checkpoints/init.neoxp-checkpoint", "existing");
        var node = new CheckpointStubNode();
        var manager = new ExpressChainManager(fileSystem, CreateSingleNodeChain());

        var act = () => manager.CreateCheckpointAsync(node, "checkpoints/init", force: false);

        await act.Should().ThrowAsync<Exception>().WithMessage("*--force*");
        node.CreateCheckpointCalls.Should().Be(0);
        fileSystem.File.ReadAllText("/work/checkpoints/init.neoxp-checkpoint").Should().Be("existing");
    }

    [Fact]
    public async Task CreateCheckpointAsync_creates_new_checkpoint()
    {
        var fileSystem = CreateFileSystem();
        var node = new CheckpointStubNode
        {
            CreateCheckpoint = path =>
            {
                fileSystem.File.WriteAllText(path, "new");
                return Task.FromResult(IExpressNode.CheckpointMode.Offline);
            }
        };
        var manager = new ExpressChainManager(fileSystem, CreateSingleNodeChain());

        var (path, _) = await manager.CreateCheckpointAsync(node, "checkpoints/init", force: false);

        fileSystem.File.ReadAllText(path).Should().Be("new");
        fileSystem.Directory.GetFiles("/work/checkpoints").Should().ContainSingle();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateCheckpointAsync_supports_long_valid_file_names(bool replaceExisting)
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), $"checkpoint-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            // The final name fits the filesystem's component limit; staging must also fit.
            var checkpointPath = Path.Combine(directory, new string('a', 230) + ".neoxp-checkpoint");
            File.WriteAllText(checkpointPath, "existing");
            if (!replaceExisting)
                File.Delete(checkpointPath);
            var node = new CheckpointStubNode
            {
                CreateCheckpoint = path =>
                {
                    File.WriteAllText(path, "replacement");
                    return Task.FromResult(IExpressNode.CheckpointMode.Offline);
                }
            };
            var manager = new ExpressChainManager(new FileSystem(), CreateSingleNodeChain());

            var (path, _) = await manager.CreateCheckpointAsync(node, checkpointPath, replaceExisting);

            path.Should().Be(checkpointPath);
            File.ReadAllText(path).Should().Be("replacement");
            Directory.GetFiles(directory).Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CreateCheckpointAsync_removes_partial_archive_when_creation_fails()
    {
        var fileSystem = CreateFileSystem();
        fileSystem.Directory.CreateDirectory("/work/checkpoints");
        fileSystem.File.WriteAllText("/work/checkpoints/init.neoxp-checkpoint", "existing");
        var node = new CheckpointStubNode
        {
            CreateCheckpoint = path =>
            {
                fileSystem.File.WriteAllText(path, "partial");
                throw new InvalidOperationException("checkpoint creation failed");
            }
        };
        var manager = new ExpressChainManager(fileSystem, CreateSingleNodeChain());

        var act = () => manager.CreateCheckpointAsync(node, "checkpoints/init", force: true);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("checkpoint creation failed");
        fileSystem.File.ReadAllText("/work/checkpoints/init.neoxp-checkpoint").Should().Be("existing");
        fileSystem.Directory.GetFiles("/work/checkpoints").Should().ContainSingle();
    }

    [Fact]
    public async Task CreateCheckpointAsync_preserves_concurrent_destination_without_force()
    {
        var fileSystem = CreateFileSystem();
        var node = new CheckpointStubNode
        {
            CreateCheckpoint = path =>
            {
                fileSystem.File.WriteAllText(path, "replacement");
                fileSystem.File.WriteAllText("/work/checkpoints/init.neoxp-checkpoint", "concurrent");
                return Task.FromResult(IExpressNode.CheckpointMode.Online);
            }
        };
        var manager = new ExpressChainManager(fileSystem, CreateSingleNodeChain());

        var act = () => manager.CreateCheckpointAsync(node, "checkpoints/init", force: false);

        await act.Should().ThrowAsync<IOException>();
        fileSystem.File.ReadAllText("/work/checkpoints/init.neoxp-checkpoint").Should().Be("concurrent");
        fileSystem.Directory.GetFiles("/work/checkpoints").Should().ContainSingle();
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
                if (path == nodePath)
                {
                    return nodeExists;
                }

                if (path == oldNodePath)
                {
                    return false;
                }

                if (nodePathBackup is not null && path == nodePathBackup)
                {
                    return backupExists;
                }

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
                if (path == nodePath)
                {
                    return nodeExists;
                }

                if (path == oldNodePath)
                {
                    return false;
                }

                if (nodePathBackup is not null && path == nodePathBackup)
                {
                    return backupExists;
                }

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

    private sealed class CheckpointStubNode : IExpressNode
    {
        public Func<string, Task<IExpressNode.CheckpointMode>> CreateCheckpoint { get; init; } =
            _ => Task.FromResult(IExpressNode.CheckpointMode.Offline);

        public int CreateCheckpointCalls { get; private set; }

        public ProtocolSettings ProtocolSettings => Neo.ProtocolSettings.Default;

        public void Dispose()
        {
        }

        public Task<IExpressNode.CheckpointMode> CreateCheckpointAsync(string checkPointPath)
        {
            CreateCheckpointCalls++;
            return CreateCheckpoint(checkPointPath);
        }

        public Task<RpcInvokeResult> InvokeAsync(Script script, Signer? signer = null) => throw new NotSupportedException();

        public Task<UInt256> ExecuteAsync(Wallet wallet, UInt160 accountHash, WitnessScope witnessScope, Script script, decimal additionalGas = 0, bool padInvokeEstimate = false) => throw new NotSupportedException();

        public Task<UInt256> SubmitOracleResponseAsync(OracleResponse response, IReadOnlyList<ECPoint> oracleNodes) => throw new NotSupportedException();

        public Task FastForwardAsync(uint blockCount, TimeSpan timestampDelta) => throw new NotSupportedException();

        public Task<Block> GetBlockAsync(UInt256 blockHash) => throw new NotSupportedException();

        public Task<Block> GetBlockAsync(uint blockIndex) => throw new NotSupportedException();

        public Task<ContractManifest> GetContractAsync(UInt160 scriptHash) => throw new NotSupportedException();

        public Task<Block> GetLatestBlockAsync() => throw new NotSupportedException();

        public Task<(Transaction tx, RpcApplicationLog? appLog)> GetTransactionAsync(UInt256 txHash) => throw new NotSupportedException();

        public Task<uint> GetTransactionHeightAsync(UInt256 txHash) => throw new NotSupportedException();

        public Task<IReadOnlyList<(TokenContract contract, BigInteger balance)>> ListBalancesAsync(UInt160 address) => throw new NotSupportedException();

        public Task<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>> ListContractsAsync() => throw new NotSupportedException();

        public Task<IReadOnlyList<string>> ListNftTokenIdsAsync(UInt160 address, UInt160 assetHash) => throw new NotSupportedException();

        public Task<IReadOnlyList<(ulong requestId, OracleRequest request)>> ListOracleRequestsAsync() => throw new NotSupportedException();

        public Task<IReadOnlyList<(string key, string value)>> ListStoragesAsync(UInt160 scriptHash) => throw new NotSupportedException();

        public Task<IReadOnlyList<TokenContract>> ListTokenContractsAsync() => throw new NotSupportedException();

        public Task<int> PersistContractAsync(ContractState state, IReadOnlyList<(string key, string value)> storagePairs, ContractCommand.OverwriteForce force) => throw new NotSupportedException();

        public Task<int> PersistStorageKeyValueAsync(UInt160 scripthash, (string key, string value) storagePair) => throw new NotSupportedException();

        public IAsyncEnumerable<(uint blockIndex, NotificationRecord notification)> EnumerateNotificationsAsync(IReadOnlySet<UInt160>? contractFilter, IReadOnlySet<string>? eventFilter) => throw new NotSupportedException();

        public Task<bool> IsNep17CompliantAsync(UInt160 contractHash) => throw new NotSupportedException();

        public Task<bool> IsNep11CompliantAsync(UInt160 contractHash) => throw new NotSupportedException();
    }
}
