// Copyright (C) 2015-2026 The Neo Project.
//
// ExpressCreateCheckpointPathTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using NeoExpress.Node;
using System;
using System.IO;
using Xunit;

namespace test.workflowvalidation;

public class ExpressCreateCheckpointPathTests
{
    static string NewBaseDirectory() =>
        Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"neoxp-base-{Guid.NewGuid():N}"));

    [Fact]
    public void Accepts_a_path_inside_the_base_directory()
    {
        var baseDir = NewBaseDirectory();
        var inside = Path.Combine(baseDir, "checkpoint.neoxp-checkpoint");

        ExpressRpcServerPlugin.EnsureCheckpointPathWithinDirectory(inside, baseDir)
            .Should().Be(Path.GetFullPath(inside));
    }

    [Fact]
    public void Rejects_a_directory_traversal_path()
    {
        var baseDir = NewBaseDirectory();
        var traversal = Path.Combine(baseDir, "..", "escape.neoxp-checkpoint");

        var act = () => ExpressRpcServerPlugin.EnsureCheckpointPathWithinDirectory(traversal, baseDir);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rejects_an_absolute_path_outside_the_base_directory()
    {
        var baseDir = NewBaseDirectory();
        var outside = Path.Combine(Path.GetFullPath(Path.GetTempPath()), $"neoxp-other-{Guid.NewGuid():N}", "x.neoxp-checkpoint");

        var act = () => ExpressRpcServerPlugin.EnsureCheckpointPathWithinDirectory(outside, baseDir);

        act.Should().Throw<ArgumentException>();
    }
}
