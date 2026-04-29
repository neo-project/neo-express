// Copyright (C) 2015-2026 The Neo Project.
//
// TransactionExecutorOracleResponseTests.cs file belongs to neo-express project and is free
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

public class TransactionExecutorOracleResponseTests
{
    [Fact]
    public async Task LoadOracleResponseJsonAsync_rejects_oversized_response_file()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>(), "/work");
        fileSystem.Directory.CreateDirectory("/work");
        var responsePath = fileSystem.Path.Combine("/work", "response.json");
        fileSystem.AddFile(responsePath, new MockFileData(new string(' ', (int)TransactionExecutor.MaxOracleResponseFileBytes + 1)));

        Func<Task> action = () => TransactionExecutor.LoadOracleResponseJsonAsync(fileSystem, responsePath, CancellationToken.None);

        var exception = await action.Should().ThrowAsync<Exception>();
        exception.Which.Message.Should().Be($"Oracle response file {responsePath} is invalid: file is larger than {TransactionExecutor.MaxOracleResponseFileBytes} bytes");
    }
}
