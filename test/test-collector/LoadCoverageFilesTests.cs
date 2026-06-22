// Copyright (C) 2015-2026 The Neo Project.
//
// LoadCoverageFilesTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Moq;
using Neo.Collector;
using System;
using System.IO;
using Xunit;

namespace test_collector;

public class LoadCoverageFilesTests
{
    [Fact]
    public void LoadCoverageFiles_ignores_missing_directory()
    {
        // When a test session produces no coverage, the coverage directory is never
        // created. Loading from a missing directory must not throw.
        var logger = new Mock<ILogger>();
        var collector = new CodeCoverageCollector(logger.Object);
        var missingPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        var exception = Record.Exception(() => collector.LoadCoverageFiles(missingPath));

        Assert.Null(exception);
    }
}
