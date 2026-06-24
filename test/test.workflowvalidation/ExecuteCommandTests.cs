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
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Commands;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace test.workflowvalidation;

public class ExecuteCommandTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Json_option_binds_from_the_command_line(bool flagSupplied)
    {
        // ExecuteCommand has no parameterless constructor, so bypass construction
        // (the factories are unused for argument parsing) and bind options directly.
        var app = new CommandLineApplication<ExecuteCommand>
        {
            ModelFactory = () => (ExecuteCommand)RuntimeHelpers.GetUninitializedObject(typeof(ExecuteCommand)),
        };
        app.Conventions.UseOptionAttributes().UseArgumentAttributes();

        app.Parse(flagSupplied ? ["somescript", "--json"] : ["somescript"]);

        app.Model.Json.Should().Be(flagSupplied);
    }

    [Fact]
    public void LoadFileScript_returns_null_for_long_non_file_input()
    {
        var method = typeof(ExecuteCommand).GetMethod("LoadFileScript", BindingFlags.NonPublic | BindingFlags.Static);
        var longInput = $"0x{new string('b', 1026)}";

        var result = method!.Invoke(null, [longInput]);

        result.Should().BeNull();
    }

    [Fact]
    public void LoadFileScript_returns_null_for_oversized_file()
    {
        var method = typeof(ExecuteCommand).GetMethod("LoadFileScript", BindingFlags.NonPublic | BindingFlags.Static);
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.nef");
        try
        {
            using (var file = File.Create(path))
            {
                file.SetLength(ExecuteCommand.MaxExecuteScriptFileBytes + 1);
            }

            var result = method!.Invoke(null, [path]);

            result.Should().BeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }
}
