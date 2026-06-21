// Copyright (C) 2015-2026 The Neo Project.
//
// TestDotNetToolTaskStdErr.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Build.Framework;
using Moq;
using Neo.BuildTasks;
using Xunit;

namespace build_tasks
{
    public class TestDotNetToolTaskStdErr
    {
        class StubTask : DotNetToolTask
        {
            protected override string Command => "nccs";
            protected override string PackageId => "Neo.Compiler.CSharp";
            protected override string GetArguments() => string.Empty;

            public StubTask(IProcessRunner processRunner) : base(processRunner) { }
        }

        // A zero exit code is success even if the tool wrote to stderr (dotnet
        // telemetry, NuGet notices, etc.). Previously TryExecute treated any
        // stderr output as a failure, which produced spurious build failures.
        [Fact]
        public void TryExecute_succeeds_on_exit_code_zero_with_stderr_output()
        {
            var expectedOutput = new[] { "tool output line" };
            var processRunner = new Mock<IProcessRunner>();
            processRunner
                .Setup(r => r.Run("dotnet", "tool list --local", It.IsAny<string>()))
                .Returns(new ProcessResults(0, expectedOutput, new[] { "a benign stderr notice" }));

            var task = new StubTask(processRunner.Object)
            {
                BuildEngine = new Mock<IBuildEngine>().Object,
            };

            var success = task.TryExecute("dotnet", "tool list --local", null, out var output);

            Assert.True(success);
            Assert.Equal(expectedOutput, output);
        }

        [Fact]
        public void TryExecute_fails_on_nonzero_exit_code()
        {
            var processRunner = new Mock<IProcessRunner>();
            processRunner
                .Setup(r => r.Run("dotnet", "tool list --local", It.IsAny<string>()))
                .Returns(new ProcessResults(1, new[] { "output" }, new[] { "an error" }));

            var task = new StubTask(processRunner.Object)
            {
                BuildEngine = new Mock<IBuildEngine>().Object,
            };

            var success = task.TryExecute("dotnet", "tool list --local", null, out var output);

            Assert.False(success);
            Assert.Empty(output);
        }
    }
}
