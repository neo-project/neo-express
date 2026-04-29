// Copyright (C) 2015-2026 The Neo Project.
//
// CreateCommandTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using NeoExpress.Commands;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace test.workflowvalidation;

public class CreateCommandTests
{
    [Fact]
    public async Task LoadBatchCommandsAsync_rejects_oversized_batch_file()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>(), "/work");
        fileSystem.Directory.CreateDirectory("/work");
        var batchPath = fileSystem.Path.Combine("/work", "init.batch");
        fileSystem.AddFile(batchPath, new MockFileData(new string(' ', (int)CreateCommand.MaxBatchFileBytes + 1)));

        Func<Task> action = () => CreateCommand.LoadBatchCommandsAsync(fileSystem, batchPath, CancellationToken.None);

        var exception = await action.Should().ThrowAsync<Exception>();
        exception.Which.Message.Should().Be($"Batch file {batchPath} is invalid: file is larger than {CreateCommand.MaxBatchFileBytes} bytes");
    }
}
