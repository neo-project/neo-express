using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

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
    private readonly List<Process> _runningProcesses = new();
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

        _output.WriteLine($"Test temp directory: {_tempDirectory}");
        _output.WriteLine($"Solution path: {_solutionPath}");
        _output.WriteLine($"Output directory: {_outDirectory}");
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
    /// Setup: Build and install neoxp tool (equivalent to pack and install steps in test.yml)
    /// </summary>
    [Fact]
    public async Task Test01_BuildAndInstallNeoxpTool()
    {
        _output.WriteLine("=== Building and Installing neoxp Tool ===");

        // Restore and build
        await RunDotNetCommand("restore", _solutionPath);
        await RunDotNetCommand("build", $"{_solutionPath} --configuration {_configuration} --no-restore");

        // Pack for install (equivalent to: dotnet pack neo-express.sln --configuration Release --output ./out --no-build)
        var packResult = await RunDotNetCommand("pack", $"{_solutionPath} --configuration {_configuration} --output {_outDirectory} --no-build --verbosity normal");
        packResult.ExitCode.Should().Be(0, "pack should succeed");

        // Verify neo.express package exists
        var packages = Directory.GetFiles(_outDirectory, "neo.express*.nupkg");
        packages.Should().NotBeEmpty("neo.express package should be created");

        // Install neoxp tool (equivalent to: dotnet tool install --add-source ./out --verbosity normal --global --prerelease neo.express)
        var installResult = await RunDotNetCommand("tool", $"install --add-source {_outDirectory} --verbosity normal --global --prerelease neo.express");

        // Tool might already be installed, so we accept both success and "already installed" scenarios
        if (installResult.ExitCode != 0 && installResult.Error.Contains("already installed"))
        {
            _output.WriteLine("Tool already installed, updating...");
            var updateResult = await RunDotNetCommand("tool", $"update --add-source {_outDirectory} --verbosity normal --global --prerelease neo.express");
            updateResult.ExitCode.Should().Be(0, "tool update should succeed");
        }
        else
        {
            installResult.ExitCode.Should().Be(0, "tool install should succeed");
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

            // Equivalent to: neoxp create
            var createResult = await RunNeoxpCommand("create");
            createResult.ExitCode.Should().Be(0, "neoxp create should succeed");

            // Verify that default.neo-express.json was created
            var configFile = Path.Combine(_tempDirectory, "default.neo-express.json");
            File.Exists(configFile).Should().BeTrue("default.neo-express.json should be created");

            // Verify the config file is valid JSON
            var configContent = await File.ReadAllTextAsync(configFile);
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

            // Equivalent to: neoxp wallet create bob
            var walletResult = await RunNeoxpCommand("wallet create bob");
            walletResult.ExitCode.Should().Be(0, "neoxp wallet create should succeed");

            // Verify wallet was created (check for wallet file or in config)
            var configFile = Path.Combine(_tempDirectory, "default.neo-express.json");
            var configContent = await File.ReadAllTextAsync(configFile);
            configContent.Should().Contain("bob", "wallet 'bob' should be added to config");

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

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempDirectory);

            // Create checkpoints directory
            var checkpointsDir = Path.Combine(_tempDirectory, "checkpoints");
            Directory.CreateDirectory(checkpointsDir);

            // Equivalent to: neoxp checkpoint create checkpoints/init --force
            var checkpointResult = await RunNeoxpCommand("checkpoint create checkpoints/init --force");
            checkpointResult.ExitCode.Should().Be(0, "neoxp checkpoint create should succeed");

            // Verify checkpoint was created
            var checkpointPath = Path.Combine(checkpointsDir, "init");
            Directory.Exists(checkpointPath).Should().BeTrue("checkpoint directory should be created");

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
        var configFile = Path.Combine(_tempDirectory, "default.neo-express.json");
        if (!File.Exists(configFile))
        {
            var originalDir = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(_tempDirectory);
                await RunNeoxpCommand("create");
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDir);
            }
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

        _output.WriteLine($"Running: dotnet {command} {arguments}");

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        _output.WriteLine($"Exit code: {process.ExitCode}");
        if (!string.IsNullOrEmpty(output))
            _output.WriteLine($"Output: {output}");
        if (!string.IsNullOrEmpty(error))
            _output.WriteLine($"Error: {error}");

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

        _output.WriteLine($"Running: neoxp {arguments}");

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        _output.WriteLine($"Exit code: {process.ExitCode}");
        if (!string.IsNullOrEmpty(output))
            _output.WriteLine($"Output: {output}");
        if (!string.IsNullOrEmpty(error))
            _output.WriteLine($"Error: {error}");

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
