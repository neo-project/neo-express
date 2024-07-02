// Copyright (C) 2015-2024 The Neo Project.
//
// ProcessRunner.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Neo.BuildTasks
{
    public readonly struct ProcessResults
    {
        public readonly int ExitCode;
        public readonly IReadOnlyCollection<string> Output;
        public readonly IReadOnlyCollection<string> Error;

        public ProcessResults(int exitCode, IReadOnlyCollection<string> output, IReadOnlyCollection<string> error)
        {
            ExitCode = exitCode;
            Output = output;
            Error = error;
        }
    }

    public interface IProcessRunner
    {
        ProcessResults Run(string command, string arguments, string workingDirectory = "");
    }

    // https://github.com/jamesmanning/RunProcessAsTask
    class ProcessRunner : IProcessRunner
    {
        public ProcessResults Run(string command, string arguments, string workingDirectory = "")
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo(command, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? "" : workingDirectory,
            };

            var process = new System.Diagnostics.Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true,
            };

            var output = new ConcurrentQueue<string>();
            process.OutputDataReceived += (sender, args) => { if (args.Data != null) { output.Enqueue(args.Data); } };

            var error = new ConcurrentQueue<string>();
            process.ErrorDataReceived += (sender, args) => { if (args.Data != null) { error.Enqueue(args.Data); } };

            var completeEvent = new ManualResetEvent(false);

            process.Exited += (sender, args) => completeEvent.Set();

            if (!process.Start())
                throw new Exception("Process failed to start");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            completeEvent.WaitOne();

            return new ProcessResults(process.ExitCode, output, error);
        }
    }
}
