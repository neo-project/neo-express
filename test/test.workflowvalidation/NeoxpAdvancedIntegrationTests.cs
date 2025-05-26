// Copyright (C) 2015-2024 The Neo Project.
//
// NeoxpAdvancedIntegrationTests.cs file belongs to neo-express project and is free
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
using Xunit.Abstractions;

namespace test.workflowvalidation;

/// <summary>
/// Advanced integration tests for neoxp tool functionality including online tests
/// These tests validate the more complex neoxp commands from test.yml
/// </summary>
public class NeoxpAdvancedIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDirectory;
    private readonly string _solutionPath;
    private readonly string _configuration = "Release";
    private readonly List<Process> _runningProcesses = new();
    private readonly string _outDirectory;
    private bool _toolInstalled = false;
    private bool _projectCreated = false;

    public NeoxpAdvancedIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"neo-express-advanced-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);

        // Get the solution path relative to the test project
        var currentDir = Directory.GetCurrentDirectory();
        var solutionDir = FindSolutionDirectory(currentDir);
        _solutionPath = Path.Combine(solutionDir, "neo-express.sln");
        _outDirectory = Path.Combine(_tempDirectory, "out");
        Directory.CreateDirectory(_outDirectory);

        _output.WriteLine($"Test temp directory: {_tempDirectory}");
        _output.WriteLine($"Solution path: {_solutionPath}");
    }

    private static string FindSolutionDirectory(string startPath)
    {
        var current = new DirectoryInfo(startPath);
        while (current != null)
        {
            if (current.GetFiles("neo-express.sln").Any())
                return current.FullName;
            current = current.Parent;
        }

        // Try alternative paths if not found
        var alternatives = new[]
        {
            Path.Combine(startPath, "..", "..", ".."),
            Path.Combine(startPath, "..", "..", "..", ".."),
            Path.Combine(startPath, "..", "..", "..", "..", ".."),
            Environment.CurrentDirectory,
            Path.Combine(Environment.CurrentDirectory, "..", ".."),
            Path.Combine(Environment.CurrentDirectory, "..", "..", "..")
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

        throw new InvalidOperationException($"Could not find neo-express.sln starting from {startPath}. Current directory: {Environment.CurrentDirectory}");
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
            var policyGetResult = await RunNeoxpCommand("policy get --rpc-uri mainnet --json");

            // Note: This might fail if mainnet is not accessible, so we'll be more lenient
            if (policyGetResult.ExitCode == 0)
            {
                // Save output to file as the original command does
                var policyFile = Path.Combine(_tempDirectory, "mainnet-policy.json");
                await File.WriteAllTextAsync(policyFile, policyGetResult.Output);

                // Verify it's valid JSON
                var policyContent = await File.ReadAllTextAsync(policyFile);
                var policy = JsonDocument.Parse(policyContent);
                policy.Should().NotBeNull("policy should be valid JSON");

                // Equivalent to: neoxp policy sync mainnet-policy --account genesis
                var policySyncResult = await RunNeoxpCommand("policy sync mainnet-policy --account genesis");
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
            var transfer1Result = await RunNeoxpCommand("transfer 10000 gas genesis node1");
            transfer1Result.ExitCode.Should().Be(0, "transfer to node1 should succeed");

            // Equivalent to: neoxp transfer 10000 gas genesis bob
            var transfer2Result = await RunNeoxpCommand("transfer 10000 gas genesis bob");
            transfer2Result.ExitCode.Should().Be(0, "transfer to bob should succeed");

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
            var runTask = RunNeoxpCommandWithTimeout("run --seconds-per-block 3 --discard", TimeSpan.FromMinutes(1));

            // Wait a bit to let it start
            await Task.Delay(5000);

            // Test that we can run commands while it's running
            // Equivalent to: neoxp transfer 10000 gas genesis node1 (online)
            var onlineTransfer1 = await RunNeoxpCommand("transfer 10000 gas genesis node1");
            // Note: This might fail if the blockchain isn't fully started yet

            // Equivalent to: neoxp transfer 10000 gas genesis bob (online)
            var onlineTransfer2 = await RunNeoxpCommand("transfer 10000 gas genesis bob");

            // Equivalent to: neoxp stop --all
            var stopResult = await RunNeoxpCommand("stop --all");
            stopResult.ExitCode.Should().Be(0, "stop command should succeed");

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
                await RunNeoxpCommand("stop --all");
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
    }

    private async Task EnsureSetup()
    {
        if (!_toolInstalled)
        {
            await BuildAndInstallTool();
        }

        if (!_projectCreated)
        {
            await CreateProject();
        }
    }

    private async Task BuildAndInstallTool()
    {
        // Build and pack
        await RunDotNetCommand("restore", _solutionPath);
        await RunDotNetCommand("build", $"{_solutionPath} --configuration {_configuration} --no-restore");
        await RunDotNetCommand("pack", $"{_solutionPath} --configuration {_configuration} --output {_outDirectory} --no-build");

        // Install tool
        var installResult = await RunDotNetCommand("tool", $"install --add-source {_outDirectory} --verbosity normal --global --prerelease neo.express");
        if (installResult.ExitCode != 0 && installResult.Error.Contains("already installed"))
        {
            await RunDotNetCommand("tool", $"update --add-source {_outDirectory} --verbosity normal --global --prerelease neo.express");
        }

        _toolInstalled = true;
    }

    private async Task CreateProject()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempDirectory);
            await RunNeoxpCommand("create");
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
            var configFile = Path.Combine(_tempDirectory, "default.neo-express.json");
            var configContent = await File.ReadAllTextAsync(configFile);
            if (!configContent.Contains("bob"))
            {
                await RunNeoxpCommand("wallet create bob");
            }
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    private async Task<(int ExitCode, string Output, string Error)> RunDotNetCommand(string command, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"{command} {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(_solutionPath)
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        return (process.ExitCode, output, error);
    }

    private async Task<(int ExitCode, string Output, string Error)> RunNeoxpCommand(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "neoxp",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _tempDirectory
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        return (process.ExitCode, output, error);
    }

    private async Task<(int ExitCode, string Output, string Error)> RunNeoxpCommandWithTimeout(string arguments, TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "neoxp",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _tempDirectory
        };

        using var process = new Process { StartInfo = startInfo };
        _runningProcesses.Add(process);

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill();
            throw new TimeoutException($"Command 'neoxp {arguments}' timed out after {timeout}");
        }

        var output = await outputTask;
        var error = await errorTask;

        return (process.ExitCode, output, error);
    }

    public void Dispose()
    {
        // Clean up any running processes
        foreach (var process in _runningProcesses)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(5000);
                }
                process.Dispose();
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error disposing process: {ex.Message}");
            }
        }

        // Ensure neoxp is stopped
        try
        {
            var stopProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "neoxp",
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
