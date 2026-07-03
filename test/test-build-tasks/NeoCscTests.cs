// Copyright (C) 2015-2026 The Neo Project.
//
// NeoCscTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BuildTasks;
using Xunit;

namespace build_tasks;

public class NeoCscTests
{
    [Fact]
    public void BuildArguments_quotes_path_derived_values()
    {
        // A project path, output directory or base name containing a space must be
        // passed to nccs as a single quoted argument, not split into tokens.
        var args = NeoCsc.BuildArguments(
            new[] { @"C:\Users\John Doe\contract.csproj" },
            @"C:\Users\John Doe\bin\sc",
            "base name",
            debug: false, assembly: false, optimize: true, inline: true, addressVersion: 53);

        Assert.Contains("\"C:\\Users\\John Doe\\contract.csproj\"", args);
        Assert.Contains("--output \"C:\\Users\\John Doe\\bin\\sc\"", args);
        Assert.Contains("--base-name \"base name\"", args);
    }

    [Theory]
    // optimize and inline are independent flags: each emits its --no- switch on its own.
    [InlineData(true, true, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, false, true)]
    [InlineData(false, false, true, true)]
    public void BuildArguments_emits_independent_optimize_and_inline_switches(
        bool optimize, bool inline, bool expectNoOptimize, bool expectNoInline)
    {
        var args = NeoCsc.BuildArguments(
            new[] { "contract.csproj" }, null, "",
            debug: false, assembly: false, optimize: optimize, inline: inline, addressVersion: 53);

        Assert.Equal(expectNoOptimize, args.Contains("--no-optimize"));
        Assert.Equal(expectNoInline, args.Contains("--no-inline"));
    }

    [Fact]
    public void BuildArguments_emits_address_version_only_when_not_default()
    {
        var defaultArgs = NeoCsc.BuildArguments(
            new[] { "contract.csproj" }, null, "",
            debug: false, assembly: false, optimize: true, inline: true, addressVersion: 53);
        var customArgs = NeoCsc.BuildArguments(
            new[] { "contract.csproj" }, null, "",
            debug: false, assembly: false, optimize: true, inline: true, addressVersion: 42);

        Assert.DoesNotContain("--address-version", defaultArgs);
        Assert.Contains("--address-version 42", customArgs);
    }
}
