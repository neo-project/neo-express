// Copyright (C) 2015-2026 The Neo Project.
//
// ContractStorageCommandTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using NeoExpress.Commands;
using Xunit;

namespace test.workflowvalidation;

public class ContractStorageCommandTests
{
    [Fact]
    public async Task WriteStoragesAsync_writes_empty_json_array_when_no_contracts_match()
    {
        using var writer = new StringWriter();
        var command = new ContractCommand.Storage.StorageGet(null!) { Json = true };

        await command.WriteStoragesAsync(null!, writer, []);

        writer.ToString().Should().Be("[]");
    }
}
