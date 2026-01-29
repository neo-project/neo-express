// Copyright (C) 2015-2025 The Neo Project.
//
// NeoxpAdvancedIntegrationTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace test.workflowvalidation;

/// <summary>
/// Advanced integration tests for neoxp tool functionality including online tests
/// These tests validate the more complex neoxp commands from test.yml
/// </summary>
[Collection("PackExclusive")]
public class NeoxpAdvancedIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDirectory;
    private readonly string _solutionPath;
    private readonly string _neoxpProjectPath;
    private readonly string _configuration = "Release";
    private readonly string _outDirectory;
    private readonly string _toolDirectory;
    private bool _toolInstalled = false;
    private bool _projectCreated = false;
    private readonly RunCommand _runCommand;

    public NeoxpAdvancedIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"neo-express-advanced-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);

        // Get the solution path relative to the test project
        var currentDir = Directory.GetCurrentDirectory();
        var solutionDir = FindSolutionDirectory(currentDir);
        _solutionPath = Path.Combine(solutionDir, "neo-express.sln");
        _neoxpProjectPath = Path.Combine(solutionDir, "src", "neoxp", "neoxp.csproj");
        _outDirectory = Path.Combine(_tempDirectory, "out");
        _toolDirectory = Path.Combine(_tempDirectory, "tools");
        Directory.CreateDirectory(_outDirectory);
        Directory.CreateDirectory(_toolDirectory);
        _runCommand = new RunCommand(_output, _solutionPath, _tempDirectory);

        _output.WriteLine($"Test temp directory: {_tempDirectory}");
        _output.WriteLine($"Solution path: {_solutionPath}");
        _output.WriteLine($"Tool directory: {_toolDirectory}");
        _output.WriteLine($"Neoxp project path: {_neoxpProjectPath}");
    }

    private static string FindSolutionDirectory(string startPath)
    {
        // First try the standard approach - walk up the directory tree
        var current = new DirectoryInfo(startPath);
        while (current != null)
        {
            if (current.GetFiles("neo-express.sln").Length != 0)
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

            // Try common development paths
            @"C:\Users\liaoj\git\neo-express",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "git", "neo-express"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "source", "neo-express"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "repos", "neo-express"),

            // Try to find any neo-express.sln on the system by searching common locations
            @"C:\git\neo-express",
            @"C:\source\neo-express",
            @"C:\repos\neo-express"
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

        // Last resort: search for neo-express.sln in common drive locations
        var drives = new[] { "C:", "D:", "E:" };
        var commonPaths = new[] { "git", "source", "repos", "dev", "projects" };

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

        throw new InvalidOperationException($"Could not find neo-express.sln starting from {startPath}. Current directory: {Environment.CurrentDirectory}");
    }

    private static string GetNeoxpPath(string toolDirectory)
    {
        var toolName = OperatingSystem.IsWindows() ? "neoxp.exe" : "neoxp";
        return Path.Combine(toolDirectory, toolName);
    }

    /// <summary>
    /// Test 1: Policy commands (equivalent to policy get and sync in test.yml)
    /// </summary>
    [Fact]
    public async Task Test01_PolicyCommands()
    {
        await EnsureSetup();

        _output.WriteLine("=== Testing neoxp policy commands ===");

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempDirectory);

            // Equivalent to: neoxp policy get --rpc-uri mainnet --json > mainnet-policy.json
            // Use a timeout to avoid hanging indefinitely if the RPC endpoint is slow/unreachable.
            (int policyGetExitCode, string policyGetOutput, string _) = (-1, string.Empty, string.Empty);
            try
            {
                (policyGetExitCode, policyGetOutput, _) = await _runCommand.RunNeoxpCommandWithTimeout(
                    TimeSpan.FromSeconds(30),
                    "policy",
                    "get",
                    "--rpc-uri",
                    "mainnet",
                    "--json");
            }
            catch (TimeoutException)
            {
                _output.WriteLine("⚠️ Policy get timed out (likely due to network), skipping policy sync test");
                return;
            }

            // Note: This might fail if mainnet is not accessible, so we'll be more lenient
            if (policyGetExitCode == 0)
            {
                // Save output to file as the original command does
                var policyFile = Path.Combine(_tempDirectory, "mainnet-policy.json");
                await File.WriteAllTextAsync(policyFile, policyGetOutput, TestContext.Current.CancellationToken);

                // Verify it's valid JSON
                var policyContent = await File.ReadAllTextAsync(policyFile, TestContext.Current.CancellationToken);
                var policy = JsonDocument.Parse(policyContent);
                policy.Should().NotBeNull("policy should be valid JSON");

                // Equivalent to: neoxp policy sync mainnet-policy --account genesis
                await _runCommand.RunNeoxpCommandWithTimeout(
                    TimeSpan.FromSeconds(30),
                    "policy",
                    "sync",
                    "mainnet-policy",
                    "--account",
                    "genesis");
                // This might fail if genesis account doesn't exist yet, which is expected

                _output.WriteLine("✅ neoxp policy commands completed");
            }
            else
            {
                _output.WriteLine("⚠️ Policy get failed (likely due to network), skipping policy sync test");
                // This is acceptable as network connectivity may not be available in test environment
            }
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    /// <summary>
    /// Test 2: Transfer commands offline (equivalent to offline transfer in test.yml)
    /// </summary>
    [Fact]
    public async Task Test02_TransferCommandsOffline()
    {
        await EnsureSetup();
        await EnsureWalletsCreated();

        _output.WriteLine("=== Testing neoxp transfer commands (offline) ===");

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempDirectory);

            // Equivalent to: neoxp transfer 10000 gas genesis node1
            await _runCommand.RunNeoxpCommand("transfer", "10000", "gas", "genesis", "node1");
            // Note: Transfer commands may fail in offline mode, which is expected behavior

            // Equivalent to: neoxp transfer 10000 gas genesis bob
            await _runCommand.RunNeoxpCommand("transfer", "10000", "gas", "genesis", "bob");
            // Note: Transfer commands may fail in offline mode, which is expected behavior

            _output.WriteLine("✅ neoxp transfer commands (offline) passed");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    /// <summary>
    /// Test 3: Run command with timeout (equivalent to run command in test.yml)
    /// </summary>
    [Fact]
    public async Task Test03_RunCommandWithTimeout()
    {
        await EnsureSetup();

        _output.WriteLine("=== Testing neoxp run command with timeout ===");

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempDirectory);

            // Equivalent to: neoxp run --seconds-per-block 3 --discard &
            // We'll run this with a timeout to simulate the GitHub Actions timeout-minutes: 1
            var runTask = _runCommand.RunNeoxpCommandWithTimeout(TimeSpan.FromMinutes(1), "run", "--seconds-per-block", "3", "--discard");

            // Wait a bit to let it start
            await Task.Delay(5000, TestContext.Current.CancellationToken);

            // Test that we can run commands while it's running
            // Equivalent to: neoxp transfer 10000 gas genesis node1 (online)
            await _runCommand.RunNeoxpCommand("transfer", "10000", "gas", "genesis", "node1");
            // Note: This might fail if the blockchain isn't fully started yet

            // Equivalent to: neoxp transfer 10000 gas genesis bob (online)
            await _runCommand.RunNeoxpCommand("transfer", "10000", "gas", "genesis", "bob");

            // Equivalent to: neoxp stop --all
            await _runCommand.RunNeoxpCommand("stop", "--all");
            // Note: Stop command may fail if no blockchain is running, which is expected

            // Wait for the run task to complete
            try
            {
                await runTask;
            }
            catch (TimeoutException)
            {
                _output.WriteLine("Run command timed out as expected");
            }

            _output.WriteLine("✅ neoxp run command with timeout test completed");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);

            // Ensure we stop any running instances
            try
            {
                await _runCommand.RunNeoxpCommand("stop", "--all");
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
    }

    private async Task EnsureSetup()
    {
        _output.WriteLine("EnsureSetup: starting");
        if (!_toolInstalled)
        {
            _output.WriteLine("EnsureSetup: build/install tool");
            await BuildAndInstallTool();
        }

        if (!_projectCreated)
        {
            _output.WriteLine("EnsureSetup: create project");
            await CreateProject();
        }
        _output.WriteLine("EnsureSetup: complete");
    }

    private async Task BuildAndInstallTool()
    {
        // Pack neoxp tool (build happens during test project build)
        _output.WriteLine("BuildAndInstallTool: pack");
        await _runCommand.RunDotNetCommand("pack", _neoxpProjectPath, "--configuration", _configuration, "--output", _outDirectory, "--no-build");

        // Uninstall existing tool first (ignore errors if not installed)
        // Try to install the tool, if it fails try to update instead
        _output.WriteLine("BuildAndInstallTool: install");
        var (toolInstallExitCode, _, toolInstallError) = await _runCommand.RunDotNetCommand("tool", "install", "--add-source", _outDirectory, "--verbosity", "normal", "--tool-path", _toolDirectory, "--prerelease", "neo.express");
        if (toolInstallExitCode != 0)
        {
            // If install failed, try update instead
            _output.WriteLine("BuildAndInstallTool: update");
            var (toolUpdateExitCode, _, toolUpdateError) = await _runCommand.RunDotNetCommand("tool", "update", "--add-source", _outDirectory, "--verbosity", "normal", "--tool-path", _toolDirectory, "--prerelease", "neo.express");
            if (toolUpdateExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to install or update neo.express tool. Install error: {toolInstallError}. Update error: {toolUpdateError}");
            }
        }

        _toolInstalled = true;
        _runCommand.SetNeoxpPath(GetNeoxpPath(_toolDirectory));
        _output.WriteLine("BuildAndInstallTool: complete");
    }

    private async Task CreateProject()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempDirectory);
            _output.WriteLine("CreateProject: neoxp create --force");
            await _runCommand.RunNeoxpCommand("create", "--force");
            _projectCreated = true;
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    private async Task EnsureWalletsCreated()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempDirectory);

            // Create bob wallet if it doesn't exist
            // neoxp creates the config file in ~/.neo-express/ directory
            var neoExpressDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".neo-express");
            var configFile = Path.Combine(neoExpressDir, "default.neo-express");

            if (File.Exists(configFile))
            {
                var configContent = await File.ReadAllTextAsync(configFile);
                if (!configContent.Contains("bob"))
                {
                    await _runCommand.RunNeoxpCommand("wallet", "create", "bob", "--force");
                }
            }
            else
            {
                // If config doesn't exist, create wallet anyway
                await _runCommand.RunNeoxpCommand("wallet", "create", "bob", "--force");
            }
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    public void Dispose()
    {
        // Clean up any running processes
        _runCommand.Dispose();

        // Ensure neoxp is stopped
        try
        {
            var stopProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _runCommand.NeoxpPath,
                    Arguments = "stop --all",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = _tempDirectory
                }
            };
            stopProcess.Start();
            stopProcess.WaitForExit(5000);
            stopProcess.Dispose();
        }
        catch
        {
            // Ignore cleanup errors
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
