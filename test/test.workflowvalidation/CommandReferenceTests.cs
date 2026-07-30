// Copyright (C) 2015-2026 The Neo Project.
//
// CommandReferenceTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Xunit;

namespace test.workflowvalidation;

public class CommandReferenceTests
{
    [Fact]
    public void policy_sync_documentation_matches_the_cli_account_option()
    {
        var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        var commandReference = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "command-reference.md"));

        commandReference.Should().Contain("Usage: neoxp policy sync [Options] <Source>");
        commandReference.Should().Contain("-a|--account <ACCOUNT>    Account to pay contract invocation GAS fee");
        commandReference.Should().NotContain("Usage: neoxp policy sync [Options] <Source> <Account>");
    }

    [Fact]
    public void public_chain_tools_document_full_state_requirement_without_promising_seed_configuration()
    {
        var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        var readme = File.ReadAllText(Path.Combine(repositoryRoot, "readme.md"));
        var traceReference = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "trace-command-reference.md"));

        readme.Should().Contain("Old state not supported");
        readme.Should().Contain("full-state StateService enabled");
        readme.Should().NotContain("official JSON-RPC nodes for MainNet and TestNet are configured");

        traceReference.Should().Contain("Old state not supported");
        traceReference.Should().Contain("full-state StateService enabled");
        traceReference.Should().NotContain("official MainNet and TestNet JSON-RPC nodes are configured");

        readme.Should().NotContain("365110");
        readme.Should().NotContain("0xef1917b8601828e1d2f3ed0954907ea611cb734771609ce0ce2b654bb5c78005");
        traceReference.Should().NotContain("365110");
        traceReference.Should().NotContain("0xef1917b8601828e1d2f3ed0954907ea611cb734771609ce0ce2b654bb5c78005");
    }

    static string FindRepositoryRoot(string startPath)
    {
        var directory = new DirectoryInfo(startPath);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "neo-express.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Could not find neo-express.sln starting from {startPath}.");
    }
}
