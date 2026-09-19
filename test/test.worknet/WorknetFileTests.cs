// Copyright (C) 2015-2026 The Neo Project.
//
// WorknetFileTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using NeoWorkNet;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.IO.Abstractions;
using Xunit;

namespace test.worknet;

public class WorknetFileTests
{
    [Fact]
    public void update_rpc_port_adds_missing_port_property()
    {
        var filename = Path.Combine(Path.GetTempPath(), $"neo-worknet-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(filename, "{\"consensus-nodes\":[{}]}");
            var fileSystem = new FileSystem();

            fileSystem.UpdateWorknetRpcPort(filename, 40332);

            var json = JObject.Parse(File.ReadAllText(filename));
            json["consensus-nodes"]![0]!["rpc-port"]!.Value<ushort>().Should().Be(40332);
        }
        finally
        {
            File.Delete(filename);
        }
    }
}
