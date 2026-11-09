// Copyright (C) 2015-2026 The Neo Project.
//
// NeoDebugInfoTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Collector.Models;
using System;
using Xunit;

namespace test_collector;

public class NeoDebugInfoTests
{
    [Fact]
    public void FromDebugInfoJson_rejects_a_scalar_root_node_with_a_FormatException()
    {
        // Fuzzing found that a JSON string root (e.g. `""`) previously caused a
        // NullReferenceException via JSONLazyCreator. A clean, descriptive
        // FormatException must be thrown instead.
        var json = SimpleJSON.JSON.Parse("\"\"");

        var ex = Record.Exception(() => NeoDebugInfo.FromDebugInfoJson(json));

        Assert.IsType<FormatException>(ex);
        Assert.Contains("object", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void test_from_debug_info_json_scalar_method_entry()
    {
        var json = SimpleJSON.JSON.Parse(
            "{\"hash\":\"0x8c23f196d8a1bfd841a4a3b9428ab48f01b3c23f\",\"methods\":[\"x\"]}");

        var ex = Record.Exception(() => NeoDebugInfo.FromDebugInfoJson(json));

        Assert.IsType<FormatException>(ex);
        Assert.Equal(
            "Invalid debug info JSON: expected a JSON object for each method entry",
            ex.Message);
    }
}
