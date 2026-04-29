// Copyright (C) 2015-2026 The Neo Project.
//
// ExecuteCommandTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using NeoExpress.Commands;
using System.Reflection;
using Xunit;

namespace test.workflowvalidation;

public class ExecuteCommandTests
{
    [Fact]
    public void LoadFileScript_returns_null_for_long_non_file_input()
    {
        var method = typeof(ExecuteCommand).GetMethod("LoadFileScript", BindingFlags.NonPublic | BindingFlags.Static);
        var longInput = $"0x{new string('b', 1026)}";

        var result = method!.Invoke(null, [longInput]);

        result.Should().BeNull();
    }
}
