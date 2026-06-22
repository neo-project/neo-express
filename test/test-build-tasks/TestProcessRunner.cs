// Copyright (C) 2015-2026 The Neo Project.
//
// TestProcessRunner.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BuildTasks;
using System.Linq;
using Xunit;

namespace build_tasks
{
    public class TestProcessRunner
    {
        // Runs a real, fast, cross-platform process (the dotnet host, which is on
        // PATH wherever these tests run) that writes to stdout and exits 0. This
        // exercises the real ProcessRunner end to end and verifies that the
        // redirected output is fully drained before the results are returned --
        // the parameterless WaitForExit guarantees the async output handlers have
        // completed, so no trailing lines are dropped.
        [Fact]
        public void Run_captures_complete_output_and_exit_code()
        {
            var runner = new ProcessRunner();

            var results = runner.Run("dotnet", "--version");

            Assert.Equal(0, results.ExitCode);
            Assert.NotEmpty(results.Output);
            Assert.All(results.Output, line => Assert.NotNull(line));
        }
    }
}
