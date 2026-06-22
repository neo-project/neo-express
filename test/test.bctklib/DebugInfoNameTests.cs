// Copyright (C) 2015-2026 The Neo Project.
//
// DebugInfoNameTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.BlockchainToolkit.Models;
using Newtonsoft.Json.Linq;
using Xunit;

namespace test.bctklib
{
    public class DebugInfoNameTests
    {
        [Fact]
        public void Parse_assigns_method_and_event_namespace_and_name()
        {
            // Debug-info names are encoded "namespace,name".
            var json = new JObject
            {
                ["hash"] = "0xf69e5188632deb3a9273519efc86cb68da8d42b8",
                ["methods"] = new JArray(new JObject
                {
                    ["id"] = "0",
                    ["name"] = "NS.Sub,DoThing",
                    ["range"] = "0-1",
                }),
                ["events"] = new JArray(new JObject
                {
                    ["id"] = "1",
                    ["name"] = "My.Namespace,Transfer",
                    ["params"] = new JArray(),
                }),
            };

            var debugInfo = DebugInfo.Parse(json);

            debugInfo.Methods[0].Namespace.Should().Be("NS.Sub");
            debugInfo.Methods[0].Name.Should().Be("DoThing");
            debugInfo.Events[0].Namespace.Should().Be("My.Namespace");
            debugInfo.Events[0].Name.Should().Be("Transfer");
        }
    }
}
