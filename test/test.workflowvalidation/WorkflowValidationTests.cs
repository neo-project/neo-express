using System.Diagnostics;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace test.workflowvalidation;

/// <summary>
/// Integration tests that replicate the GitHub Actions workflow in test.yml
/// These tests validate the same functionality as the CI/CD pipeline
/// </summary>
public class WorkflowValidationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDirectory;
    private readonly string _solutionPath;
    private readonly string _configuration = "Release";
    private readonly List<Process> _runningProcesses = new();

    public WorkflowValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"neo-express-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);

        // Get the solution path relative to the test project
        var currentDir = Directory.GetCurrentDirectory();
        var solutionDir = FindSolutionDirectory(currentDir);
        _solutionPath = Path.Combine(solutionDir, "neo-express.sln");

        _output.WriteLine($"Test temp directory: {_tempDirectory}");
        _output.WriteLine($"Solution path: {_solutionPath}");
        _output.WriteLine($"Current directory: {currentDir}");
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
    /// Test 1: Format validation (equivalent to format job in test.yml)
    /// </summary>
    [Fact]
    public async Task Test01_FormatValidation()
    {
        _output.WriteLine("=== Testing Format Validation ===");

        // Equivalent to: dotnet restore neo-express.sln
        var restoreResult = await RunDotNetCommand("restore", _solutionPath);
        restoreResult.ExitCode.Should().Be(0, "restore should succeed");

        // Equivalent to: dotnet format neo-express.sln --verify-no-changes --no-restore --verbosity diagnostic
        var formatResult = await RunDotNetCommand("format", $"{_solutionPath} --verify-no-changes --no-restore --verbosity diagnostic");
        formatResult.ExitCode.Should().Be(0, "format verification should pass");

        _output.WriteLine("✅ Format validation passed");
    }

    /// <summary>
    /// Test 2: Build validation (equivalent to build job in test.yml)
    /// </summary>
    [Fact]
    public async Task Test02_BuildValidation()
    {
        _output.WriteLine("=== Testing Build Validation ===");

        // Equivalent to: dotnet restore neo-express.sln
        var restoreResult = await RunDotNetCommand("restore", _solutionPath);
        restoreResult.ExitCode.Should().Be(0, "restore should succeed");

        // Equivalent to: dotnet build neo-express.sln --configuration Release --no-restore --verbosity normal
        var buildResult = await RunDotNetCommand("build", $"{_solutionPath} --configuration {_configuration} --no-restore --verbosity normal");
        buildResult.ExitCode.Should().Be(0, "build should succeed");
        buildResult.Output.Should().Contain("Build succeeded", "build should complete successfully");

        _output.WriteLine("✅ Build validation passed");
    }

    /// <summary>
    /// Test 3: Unit test validation (equivalent to test step in test.yml)
    /// </summary>
    [Fact]
    public async Task Test03_UnitTestValidation()
    {
        _output.WriteLine("=== Testing Unit Test Validation ===");

        // Build first
        await RunDotNetCommand("restore", _solutionPath);
        await RunDotNetCommand("build", $"{_solutionPath} --configuration {_configuration} --no-restore");

        // Equivalent to: dotnet test neo-express.sln --configuration Release --no-build --verbosity normal
        var testResult = await RunDotNetCommand("test", $"{_solutionPath} --configuration {_configuration} --no-build --verbosity normal");
        testResult.ExitCode.Should().Be(0, "all tests should pass");
        testResult.Output.Should().Contain("Test summary:", "test summary should be present");
        testResult.Output.Should().Contain("succeeded", "tests should succeed");

        _output.WriteLine("✅ Unit test validation passed");
    }

    /// <summary>
    /// Test 4: Pack validation (equivalent to pack step in test.yml)
    /// </summary>
    [Fact]
    public async Task Test04_PackValidation()
    {
        _output.WriteLine("=== Testing Pack Validation ===");

        var outDir = Path.Combine(_tempDirectory, "out");
        Directory.CreateDirectory(outDir);

        // Build first
        await RunDotNetCommand("restore", _solutionPath);
        await RunDotNetCommand("build", $"{_solutionPath} --configuration {_configuration} --no-restore");

        // Equivalent to: dotnet pack neo-express.sln --configuration Release --output ./out --no-build --verbosity normal
        var packResult = await RunDotNetCommand("pack", $"{_solutionPath} --configuration {_configuration} --output {outDir} --no-build --verbosity normal");
        packResult.ExitCode.Should().Be(0, "pack should succeed");

        // Verify packages were created
        var packages = Directory.GetFiles(outDir, "*.nupkg");
        packages.Should().NotBeEmpty("at least one package should be created");
        packages.Should().Contain(p => Path.GetFileName(p).StartsWith("neo.express"), "neo.express package should be created");

        _output.WriteLine($"✅ Pack validation passed - created {packages.Length} packages");
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
