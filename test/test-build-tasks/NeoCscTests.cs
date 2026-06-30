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
}
