// Copyright (C) 2015-2026 The Neo Project.
//
// TestNeoBuildTasksTargets.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit;

namespace build_tasks
{
    public class TestNeoBuildTasksTargets
    {
        static string FindTargetsPath()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "src", "build-tasks", "build", "Neo.BuildTasks.targets");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                dir = dir.Parent;
            }

            throw new FileNotFoundException("Could not find Neo.BuildTasks.targets");
        }

        [Fact]
        public void express_batch_stamp_stays_inside_intermediate_output()
        {
            var text = File.ReadAllText(FindTargetsPath());
            Assert.Contains("GetFileName($(NeoExpressBatchFile))", text);
            Assert.Contains("FileWrites Include=\"$(NeoExpressTouchFile)\"", text);
            Assert.Contains("FileWrites Include=\"@(NeoCscOutput)\"", text);
            Assert.Contains("BeforeTargets=\"CoreClean;Rebuild\"", text);
            Assert.Contains("NeoExpressLegacyTouchFile", text);
        }

        [Fact]
        public void express_batch_incremental_inputs_do_not_include_the_stamp()
        {
            var text = File.ReadAllText(FindTargetsPath());
            Assert.Contains("Inputs=\"@(NeoCscOutput);$(NeoExpressNormalizedBatchFile);$(NeoExpressNormalizedInputFile)\"", text);
            Assert.Contains("Outputs=\"$(NeoExpressTouchFile)\"", text);
            Assert.DoesNotContain("Inputs=\"@(NeoCscOutput);$(NeoExpressBatchFile);$(NeoExpressTouchFile)\"", text);
        }

        [Fact]
        public void clean_deletes_express_batch_stamp_for_relative_batch_paths()
        {
            var targetsPath = FindTargetsPath();
            var temp = Directory.CreateTempSubdirectory("neoxp-buildtasks-");
            try
            {
                var projectDir = Path.Combine(temp.FullName, "src");
                Directory.CreateDirectory(projectDir);
                var intermediate = Path.Combine(projectDir, "obj", "Debug", "net10.0");
                var legacyDir = Path.Combine(projectDir, "obj", "Debug");
                Directory.CreateDirectory(intermediate);
                Directory.CreateDirectory(legacyDir);

                var stamp = Path.Combine(intermediate, "express.batch.neoxp.touch");
                var legacyStamp = Path.Combine(legacyDir, "express.batch.neoxp.touch");
                File.WriteAllText(stamp, "stamp");
                File.WriteAllText(legacyStamp, "legacy");

                var projectPath = Path.Combine(projectDir, "clean.proj");
                File.WriteAllText(projectPath, $"""
                    <Project>
                      <PropertyGroup>
                        <Configuration>Debug</Configuration>
                        <BaseIntermediateOutputPath>{Path.Combine(projectDir, "obj") + Path.DirectorySeparatorChar}</BaseIntermediateOutputPath>
                        <IntermediateOutputPath>{intermediate + Path.DirectorySeparatorChar}</IntermediateOutputPath>
                        <NeoExpressBatchFile>../express.batch</NeoExpressBatchFile>
                        <NeoBuildTasksAssembly>{Path.Combine(temp.FullName, "missing.dll")}</NeoBuildTasksAssembly>
                      </PropertyGroup>
                      <Import Project="{targetsPath}" />
                    </Project>
                    """);

                var psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"msbuild \"{projectPath}\" -nologo -t:CleanNeoExpressBatch -v:q",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start msbuild");
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                Assert.True(process.ExitCode == 0, stdout + Environment.NewLine + stderr);
                Assert.False(File.Exists(stamp), "Current NeoExpressTouchFile should be deleted on Clean");
                Assert.False(File.Exists(legacyStamp), "Legacy obj/Configuration stamp should be deleted on Clean");
            }
            finally
            {
                try
                { temp.Delete(true); }
                catch { }
            }
        }
    }
}
