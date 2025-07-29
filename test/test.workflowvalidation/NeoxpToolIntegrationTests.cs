// Copyright (C) 2015-2025 The Neo Project.
//
// NeoxpToolIntegrationTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace test.workflowvalidation;

/// <summary>
/// Integration tests for neoxp tool functionality (equivalent to neoxp commands in test.yml)
/// These tests validate the same neoxp tool commands as the CI/CD pipeline
/// </summary>
public class NeoxpToolIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDirectory;
    private readonly string _solutionPath;
    private readonly string _configuration = "Release";
    private readonly RunCommand _runCommand;
    private readonly string _outDirectory;
    private bool _toolInstalled = false;

    public NeoxpToolIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"neo-express-tool-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);

        // Get the solution path relative to the test project
        var currentDir = Directory.GetCurrentDirectory();
        var solutionDir = FindSolutionDirectory(currentDir);
        _solutionPath = Path.Combine(solutionDir, "neo-express.sln");
        _outDirectory = Path.Combine(_tempDirectory, "out");
        Directory.CreateDirectory(_outDirectory);
        _runCommand = new RunCommand(_output, _solutionPath, _tempDirectory);

        _output.WriteLine($"Test temp directory: {_tempDirectory}");
        _output.WriteLine($"Solution path: {_solutionPath}");
        _output.WriteLine($"Output directory: {_outDirectory}");
    }

    private static string FindSolutionDirectory(string startPath)
    {
        // First try the standard approach - walk up the directory tree
        var current = new DirectoryInfo(startPath);
        while (current != null)
        {
            if (current.GetFiles("neo-express.sln").Any())
                return current.FullName;
            current = current.Parent;
        }

        // If running from a temp directory (like when dotnet test runs), try to find the solution
        // by looking for common development paths
        var alternatives = new[]
        {
            // Try relative paths from start path
            Path.Combine(startPath, "..", "..", ".."),
            Path.Combine(startPath, "..", "..", "..", ".."),
            Path.Combine(startPath, "..", "..", "..", "..", ".."),

            // Try from current directory
            Environment.CurrentDirectory,
            Path.Combine(Environment.CurrentDirectory, "..", ".."),
            Path.Combine(Environment.CurrentDirectory, "..", "..", ".."),

            // Try common development paths (Windows)
            @"C:\Users\liaoj\git\neo-express",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "git", "neo-express"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "source", "neo-express"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "repos", "neo-express"),

            // Try to find any neo-express.sln on the system by searching common locations (Windows)
            @"C:\git\neo-express",
            @"C:\source\neo-express",
            @"C:\repos\neo-express",

            // GitHub Actions runner paths (macOS/Linux)
            "/Users/runner/work/neo-express/neo-express",
            "/home/runner/work/neo-express/neo-express",

            // Common macOS/Linux development paths
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "work", "neo-express", "neo-express"),
            "/opt/neo-express",
            "/usr/local/src/neo-express"
        };

        foreach (var alt in alternatives)
        {
            try
            {
                var fullPath = Path.GetFullPath(alt);
                if (File.Exists(Path.Combine(fullPath, "neo-express.sln")))
                    return fullPath;
            }
            catch
            {
                // Ignore path errors
            }
        }

        // Last resort: search for neo-express.sln in common drive/root locations
        var commonPaths = new[] { "git", "source", "repos", "dev", "projects", "work" };

        // Windows drive search
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            var drives = new[] { "C:", "D:", "E:" };
            foreach (var drive in drives)
            {
                foreach (var commonPath in commonPaths)
                {
                    try
                    {
                        var searchPath = Path.Combine(drive, commonPath, "neo-express");
                        if (Directory.Exists(searchPath) && File.Exists(Path.Combine(searchPath, "neo-express.sln")))
                            return searchPath;
                    }
                    catch
                    {
                        // Ignore path errors
                    }
                }
            }
        }
        else
        {
            // macOS/Linux root search
            var rootPaths = new[] { "/", "/opt", "/usr/local", "/home", "/Users" };
            foreach (var rootPath in rootPaths)
            {
                foreach (var commonPath in commonPaths)
                {
                    try
                    {
                        var searchPath = Path.Combine(rootPath, commonPath, "neo-express");
                        if (Directory.Exists(searchPath) && File.Exists(Path.Combine(searchPath, "neo-express.sln")))
                            return searchPath;
                    }
                    catch
                    {
                        // Ignore path errors
                    }
                }
            }
        }

        throw new InvalidOperationException($"Could not find neo-express.sln starting from {startPath}. Current directory: {Environment.CurrentDirectory}");
    }

    /// <summary>
    /// Setup: Build and install neoxp tool (equivalent to pack and install steps in test.yml)
    /// </summary>
    [Fact]
    public async Task Test01_BuildAndInstallNeoxpTool()
    {
        _output.WriteLine("=== Building and Installing neoxp Tool ===");

        // Restore and build
        await _runCommand.RunDotNetCommand("restore", _solutionPath);
        await _runCommand.RunDotNetCommand("build", _solutionPath, "--configuration", _configuration, "--no-restore");

        // Pack for install (equivalent to: dotnet pack neo-express.sln --configuration Release --output ./out --no-build)
        var (packExitCode, _, _) = await _runCommand.RunDotNetCommand("pack", _solutionPath, "--configuration", _configuration, "--output", _outDirectory, "--no-build", "--verbosity", "normal");
        packExitCode.Should().Be(0, "pack should succeed");

        // Verify neo.express package exists
        var packages = Directory.GetFiles(_outDirectory, "neo.express*.nupkg");
        packages.Should().NotBeEmpty("neo.express package should be created");

        // Try to uninstall any existing tool first to avoid conflicts
        _output.WriteLine("Uninstalling any existing neo.express tool...");
        await _runCommand.RunDotNetCommand("tool", "uninstall", "--global", "neo.express");

        // Install neoxp tool (equivalent to: dotnet tool install --add-source ./out --verbosity normal --global --prerelease neo.express)
        var (toolInstallExitCode, _, toolInstallError) = await _runCommand.RunDotNetCommand("tool", "install", "--add-source", _outDirectory, "--verbosity", "normal", "--global", "--prerelease", "neo.express");

        // Handle various installation scenarios
        if (toolInstallExitCode != 0)
        {
            if (toolInstallError.Contains("already installed") ||
                toolInstallError.Contains("already exists") ||
                toolInstallError.Contains("file or directory with the same name already exists"))
            {
                _output.WriteLine("Tool already exists, trying to update...");
                var (toolUpdateExitCode, _, _) = await _runCommand.RunDotNetCommand("tool", "update", "--add-source", _outDirectory, "--verbosity", "normal", "--global", "--prerelease", "neo.express");
                if (toolUpdateExitCode != 0)
                {
                    _output.WriteLine("Update failed, trying uninstall and reinstall...");
                    await _runCommand.RunDotNetCommand("tool", "uninstall", "--global", "neo.express");
                    await Task.Delay(1000, TestContext.Current.CancellationToken); // Wait for cleanup
                    var (toolReinstallExitCode, _, _) = await _runCommand.RunDotNetCommand("tool", "install", "--add-source", _outDirectory, "--verbosity", "normal", "--global", "--prerelease", "neo.express");
                    toolReinstallExitCode.Should().Be(0, "tool reinstall should succeed");
                }
            }
            else
            {
                toolInstallExitCode.Should().Be(0, "tool install should succeed");
            }
        }

        _toolInstalled = true;
        _output.WriteLine("✅ neoxp tool installed successfully");
    }

    /// <summary>
    /// Test 2: Create command (equivalent to: neoxp create)
    /// </summary>
    [Fact]
    public async Task Test02_CreateCommand()
    {
        await EnsureToolInstalled();

        _output.WriteLine("=== Testing neoxp create command ===");

        // Change to temp directory for test
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempDirectory);

            // Equivalent to: neoxp create --force
            var (createExitCode, _, _) = await _runCommand.RunNeoxpCommand("create", "--force");
            createExitCode.Should().Be(0, "neoxp create should succeed");

            // Verify that default.neo-express was created in ~/.neo-express/ directory
            var neoExpressDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".neo-express");
            var configFile = Path.Combine(neoExpressDir, "default.neo-express");
            File.Exists(configFile).Should().BeTrue("default.neo-express should be created in ~/.neo-express/");

            // Verify the config file is valid JSON
            var configContent = await File.ReadAllTextAsync(configFile, TestContext.Current.CancellationToken);
            var config = JsonDocument.Parse(configContent);
            config.RootElement.TryGetProperty("magic", out _).Should().BeTrue("config should have magic property");

            _output.WriteLine("✅ neoxp create command passed");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    /// <summary>
    /// Test 3: Wallet create command (equivalent to: neoxp wallet create bob)
    /// </summary>
    [Fact]
    public async Task Test03_WalletCreateCommand()
    {
        await EnsureToolInstalled();
        await EnsureProjectCreated();

        _output.WriteLine("=== Testing neoxp wallet create command ===");

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempDirectory);

            // Equivalent to: neoxp wallet create bob --force
            var (walletCreateExitCode, walletCreateOutput, _) = await _runCommand.RunNeoxpCommand("wallet", "create", "bob", "--force");
            walletCreateExitCode.Should().Be(0, "neoxp wallet create should succeed");

            // Verify wallet was created (check for wallet file or in config)
            // neoxp creates the config file in ~/.neo-express/ directory
            var neoExpressDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".neo-express");
            var configFile = Path.Combine(neoExpressDir, "default.neo-express");

            // Check if config file exists and contains the wallet
            if (File.Exists(configFile))
            {
                var configContent = await File.ReadAllTextAsync(configFile, TestContext.Current.CancellationToken);
                configContent.Should().Contain("bob", "wallet 'bob' should be added to config");
            }
            else
            {
                // Alternative: check if the wallet was created successfully by parsing the command output
                walletCreateOutput.Should().Contain("Created Wallet bob", "wallet creation should be confirmed in output");
            }

            _output.WriteLine("✅ neoxp wallet create command passed");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    /// <summary>
    /// Test 4: Checkpoint create command (equivalent to: neoxp checkpoint create checkpoints/init --force)
    /// </summary>
    [Fact]
    public async Task Test04_CheckpointCreateCommand()
    {
        await EnsureToolInstalled();
        await EnsureProjectCreated();

        _output.WriteLine("=== Testing neoxp checkpoint create command ===");

        // Clean up any existing RocksDB lock files before starting
        await CleanupRocksDbLockFiles();

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempDirectory);

            // Create checkpoints directory
            var checkpointsDir = Path.Combine(_tempDirectory, "checkpoints");
            Directory.CreateDirectory(checkpointsDir);

            // Stop any running nodes first to release locks
            _output.WriteLine("Stopping any running nodes to release RocksDB locks...");
            await _runCommand.RunNeoxpCommand("stop", "--all");
            await Task.Delay(2000, TestContext.Current.CancellationToken); // Wait for processes to fully stop

            // Equivalent to: neoxp checkpoint create checkpoints/init --force
            var (checkpointCreateExitCode, _, _) = await _runCommand.RunNeoxpCommand("checkpoint", "create", "checkpoints/init", "--force");
            checkpointCreateExitCode.Should().Be(0, "neoxp checkpoint create should succeed");

            // Verify checkpoint was created (neoxp creates a .neoxp-checkpoint file, not a directory)
            var checkpointFile = Path.Combine(checkpointsDir, "init.neoxp-checkpoint");
            File.Exists(checkpointFile).Should().BeTrue("checkpoint file should be created");

            _output.WriteLine("✅ neoxp checkpoint create command passed");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    private async Task EnsureToolInstalled()
    {
        if (!_toolInstalled)
        {
            await Test01_BuildAndInstallNeoxpTool();
        }
    }

    private async Task EnsureProjectCreated()
    {
        // neoxp creates the config file in ~/.neo-express/ directory
        var neoExpressDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".neo-express");
        var configFile = Path.Combine(neoExpressDir, "default.neo-express");

        if (!File.Exists(configFile))
        {
            var originalDir = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(_tempDirectory);
                await _runCommand.RunNeoxpCommand("create --force");
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDir);
            }
        }
    }

    private async Task CleanupRocksDbLockFiles()
    {
        try
        {
            _output.WriteLine("Cleaning up RocksDB lock files...");

            // Stop any running neoxp processes first
            await _runCommand.RunNeoxpCommand("stop", "--all");
            await Task.Delay(1000); // Wait for processes to stop

            // Clean up the neo-express directory which contains RocksDB files
            var neoExpressDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".neo-express");
            if (Directory.Exists(neoExpressDir))
            {
                var blockchainNodesDir = Path.Combine(neoExpressDir, "blockchain-nodes");
                if (Directory.Exists(blockchainNodesDir))
                {
                    _output.WriteLine($"Removing blockchain nodes directory: {blockchainNodesDir}");
                    Directory.Delete(blockchainNodesDir, true);
                }
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Warning: Could not clean up RocksDB lock files: {ex.Message}");
        }
    }

    public void Dispose()
    {
        // Stop any running neoxp processes first
        try
        {
            var stopTask = _runCommand.RunNeoxpCommand("stop --all");
            stopTask.Wait(5000); // Wait up to 5 seconds
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Warning: Could not stop neoxp processes: {ex.Message}");
        }

        // Clean up any running processes
        _runCommand.Dispose();

        // Clean up RocksDB files
        try
        {
            var neoExpressDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".neo-express");
            if (Directory.Exists(neoExpressDir))
            {
                var blockchainNodesDir = Path.Combine(neoExpressDir, "blockchain-nodes");
                if (Directory.Exists(blockchainNodesDir))
                {
                    _output.WriteLine($"Cleaning up blockchain nodes directory: {blockchainNodesDir}");
                    Directory.Delete(blockchainNodesDir, true);
                }
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Warning: Could not clean up RocksDB files: {ex.Message}");
        }

        // Clean up temp directory
        try
        {
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, true);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Error cleaning up temp directory: {ex.Message}");
        }
    }
}
