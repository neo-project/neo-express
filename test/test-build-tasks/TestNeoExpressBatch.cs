// Copyright (C) 2015-2026 The Neo Project.
//
// TestNeoExpressBatch.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BuildTasks;
using System.Collections.Generic;
using Xunit;

namespace build_tasks
{
    public class TestNeoExpressBatch
    {
        [Fact]
        public void parse_executable_lines_skips_comments()
        {
            var batch = """
                # setup
                transfer 1 gas genesis node1
                contract deploy ./bin/sc/Contract.nef genesis
                contract deploy "./path with space/Nep17Contract.nef" owner --gas 2
                checkpoint create ./checkpoints/deployed -f
                // done
                """;

            var lines = NeoExpressBatch.ParseExecutableLines(batch);
            Assert.Equal(4, lines.Count);
            Assert.Equal("transfer 1 gas genesis node1", lines[0]);
            Assert.Equal("contract deploy ./bin/sc/Contract.nef genesis", lines[1]);
        }

        [Fact]
        public void split_command_line_preserves_quoted_paths_and_deploy_options()
        {
            var parts = NeoExpressBatch.SplitCommandLine(
                "contract deploy \"./path with space/Contract.nef\" genesis --gas 2 --data \"{}\"");

            Assert.Equal(new[]
            {
                "contract", "deploy", "./path with space/Contract.nef", "genesis",
                "--gas", "2", "--data", "{}"
            }, parts);
            Assert.True(NeoExpressBatch.IsContractDeployLine(parts));
        }

        [Fact]
        public void build_deploy_command_does_not_inject_force()
        {
            var parts = NeoExpressBatch.SplitCommandLine(
                "contract deploy \"./path with space/Contract.nef\" genesis --gas 2");

            var command = NeoExpressBatch.BuildDeployCommand(parts, "default.neo-express", trace: false);

            Assert.Equal(
                "contract deploy \"./path with space/Contract.nef\" genesis --gas 2 --input default.neo-express",
                command);
            Assert.DoesNotContain("--force", command);
        }

        [Fact]
        public void build_deploy_command_keeps_force_when_the_batch_line_has_it()
        {
            var parts = NeoExpressBatch.SplitCommandLine(
                "contract deploy ./bin/sc/Contract.nef genesis --force");

            var command = NeoExpressBatch.BuildDeployCommand(parts, null, trace: true);

            Assert.Equal("contract deploy ./bin/sc/Contract.nef genesis --force --trace", command);
        }

        [Fact]
        public void chain_running_failure_is_detected_from_stderr()
        {
            var results = new ProcessResults(
                1,
                new List<string>(),
                new List<string> { "Cannot run batch command while blockchain is running" });
            Assert.True(NeoExpressBatch.IsChainRunningFailure(results));
        }

        [Fact]
        public void unrelated_batch_failure_is_not_treated_as_running_chain()
        {
            var results = new ProcessResults(
                1,
                new List<string> { "Batch file missing" },
                new List<string>());
            Assert.False(NeoExpressBatch.IsChainRunningFailure(results));
        }
    }
}
