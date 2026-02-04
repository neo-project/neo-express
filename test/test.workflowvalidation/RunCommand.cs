// Copyright (C) 2015-2026 The Neo Project.
//
// RunCommand.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Diagnostics;
using System.Globalization;
using Xunit;

namespace test.workflowvalidation;

public sealed class RunCommand(ITestOutputHelper output, string solutionPath, string tempDirectory) : IDisposable
{
    private const string COMMAND_DOTNET = "dotnet";
    private const string COMMAND_NEOXP = "neoxp";
    private readonly ITestOutputHelper _output = output;
    private readonly string _tempDirectory = tempDirectory;
    private readonly string _workingDirectory = Path.GetDirectoryName(solutionPath);
    private readonly List<Process> _runningProcesses = [];
    private string LANGUAGE = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;

    public void SetLanguage(string language)
    {
        LANGUAGE = language;
    }

    private async Task<(int ExitCode, string Output, string Error)> RunProcess(string fileName, string? command = null, TimeSpan? timeout = null, params string[] args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName, nameof(fileName));
        string directory = fileName.Equals(COMMAND_DOTNET) ? _workingDirectory : _tempDirectory;

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = directory
        };

        psi.Environment["DOTNET_CLI_UI_LANGUAGE"] = LANGUAGE;

        if (!string.IsNullOrWhiteSpace(command))
        {
            psi.ArgumentList.Add(command);
        }

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = psi };
        _runningProcesses.Add(process);

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        if (timeout != null)
        {
            try
            {
                using var cts = new CancellationTokenSource(timeout.Value);
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill();
                throw new TimeoutException($"Command '{fileName} {args}' timed out after {timeout}");
            }
        }
        else
        {
            await process.WaitForExitAsync();
        }

        var output = await outputTask;
        var error = await errorTask;

        _output.WriteLine($"Exit Code: {process.ExitCode}");
        if (!string.IsNullOrWhiteSpace(output))
            _output.WriteLine($"Output: {output}");
        if (!string.IsNullOrWhiteSpace(error))
            _output.WriteLine($"Error: {error}");

        psi.Environment["DOTNET_CLI_UI_LANGUAGE"] = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;

        return (process.ExitCode, output, error);
    }

    internal async Task<(int ExitCode, string Output, string Error)> RunDotNetCommand(string command, params string[] arguments)
    {
        var (exitCode, output, error) = await RunProcess(COMMAND_DOTNET, command, null, arguments);
        return (exitCode, output, error);
    }

    internal async Task<(int ExitCode, string Output, string Error)> RunNeoxpCommand(params string[] arguments)
    {
        var (exitCode, output, error) = await RunProcess(COMMAND_NEOXP, null, null, arguments);
        return (exitCode, output, error);
    }

    internal async Task<(int ExitCode, string Output, string Error)> RunNeoxpCommandWithTimeout(TimeSpan timeout, params string[] arguments)
    {
        var (exitCode, output, error) = await RunProcess(COMMAND_NEOXP, null, timeout, arguments);
        return (exitCode, output, error);
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
    }
}
