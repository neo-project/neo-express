// Copyright (C) 2015-2026 The Neo Project.
//
// ToolkitConsensusNodeTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using Xunit;

namespace test.bctklib
{
    public class ToolkitConsensusNodeTests
    {
        [Fact]
        public void WriteJson_round_trips_rpc_port()
        {
            var wallet = new ToolkitWallet("test", ProtocolSettings.Default);
            var node = new ToolkitConsensusNode(wallet, (ushort)50011, (ushort)50012);

            var sw = new StringWriter();
            using (var jw = new JsonTextWriter(sw))
            {
                node.WriteJson(jw);
            }
            var parsed = ToolkitConsensusNode.Parse(JObject.Parse(sw.ToString()), ProtocolSettings.Default);

            parsed.TcpPort.Should().Be(50011);
            parsed.RpcPort.Should().Be(50012);
        }
    }
}
