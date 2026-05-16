// Copyright (C) 2015-2026 The Neo Project.
//
// CheckpointFixtureTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using NeoTestHarness;
using Xunit;

namespace test.harness;

public class CheckpointFixtureTests
{
    [Fact]
    public void Constructor_reports_missing_relative_checkpoint_path()
    {
        var checkpointPath = Path.Combine(Guid.NewGuid().ToString("N"), "missing.neoxp-checkpoint");

        var action = () => new MissingCheckpointFixture(checkpointPath);

        action.Should().Throw<FileNotFoundException>()
            .WithMessage("couldn't find checkpoint*")
            .Where(exception => exception.FileName == checkpointPath);
    }

    private sealed class MissingCheckpointFixture : CheckpointFixture
    {
        public MissingCheckpointFixture(string checkpointPath)
            : base(checkpointPath)
        {
        }
    }
}
