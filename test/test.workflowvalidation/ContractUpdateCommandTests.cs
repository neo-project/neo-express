// Copyright (C) 2015-2026 The Neo Project.
//
// ContractUpdateCommandTests.cs file belongs to neo-express project and is free
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

public class ContractUpdateCommandTests
{
    [Fact]
    public void ParseUpdateData_returns_null_when_data_is_omitted()
    {
        var data = ContractCommand.Update.ParseUpdateData(string.Empty, _ => throw new InvalidOperationException());

        data.Should().BeNull();
    }

    [Fact]
    public void ParseUpdateData_parses_explicit_data()
    {
        var data = ContractCommand.Update.ParseUpdateData("42", value => value.Length);

        data.Should().Be(2);
    }
}
