// Copyright (C) 2015-2026 The Neo Project.
//
// ExpressChainManagerCheckpointTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using NeoExpress;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace test.workflowvalidation;

public class ExpressChainManagerCheckpointTests
{
    [Fact]
    public void ResolveCheckpointFileName_allows_relative_subdirectory_paths()
    {
        var fileSystem = CreateFileSystem();

        var checkpointPath = ExpressChainManager.ResolveCheckpointFileName(fileSystem, "checkpoints/init");

        checkpointPath.Should().Be(fileSystem.Path.Combine("/work", "checkpoints", "init.neoxp-checkpoint"));
    }

    [Theory]
    [InlineData("../../../../etc/passwd")]
    [InlineData("/tmp/passwd")]
    public void ResolveCheckpointFileName_rejects_paths_outside_current_directory(string path)
    {
        var fileSystem = CreateFileSystem();

        var action = () => ExpressChainManager.ResolveCheckpointFileName(fileSystem, path);

        action.Should().Throw<ArgumentException>()
            .WithMessage("Checkpoint path must stay within the current directory*");
    }

    private static MockFileSystem CreateFileSystem()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>(), "/work");
        fileSystem.Directory.CreateDirectory("/work");
        fileSystem.Directory.SetCurrentDirectory("/work");
        return fileSystem;
    }
}
