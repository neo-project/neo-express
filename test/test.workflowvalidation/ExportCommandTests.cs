// Copyright (C) 2015-2026 The Neo Project.
//
// ExportCommandTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit;
using Neo.Network.P2P.Payloads;
using NeoExpress;
using NeoExpress.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using Xunit;

namespace test.workflowvalidation;

public class ExportCommandTests
{
    [Fact]
    public void export_writes_resolved_protocol_configuration_for_neo_cli()
    {
        var chain = ExpressChainManagerFactory.CreateChain(1, null);
        chain.Settings["protocol.MillisecondsPerBlock"] = "3000";
        chain.Settings["protocol.MaxTransactionsPerBlock"] = "1024";
        chain.Settings["protocol.MemoryPoolMaxTransactions"] = "60000";
        chain.Settings["protocol.MaxTraceableBlocks"] = "1000";
        chain.Settings["protocol.MaxValidUntilBlockIncrement"] = "100";
        chain.Settings["protocol.InitialGasDistribution"] = "123456789";
        chain.Settings["protocol.Hardforks.HF_Echidna"] = "0";
        chain.Settings["protocol.Hardforks.HF_Faun"] = "42";
        chain.Settings["protocol.Hardforks.HF_Gorgon"] = "42";
        chain.Settings["protocol.Hardforks.HF_Huyao"] = "84";
        var settings = chain.GetProtocolSettings();

        using var textWriter = new StringWriter();
        using (var jsonWriter = new JsonTextWriter(textWriter))
        {
            jsonWriter.WriteStartObject();
            ExportCommand.WriteProtocolConfiguration(jsonWriter, settings);
            jsonWriter.WriteEndObject();
        }

        var json = JObject.Parse(textWriter.ToString());
        var protocol = json["ProtocolConfiguration"]!;
        protocol["Magic"].Should().BeNull();
        protocol["Network"]!.Value<uint>().Should().Be(chain.Network);
        protocol["AddressVersion"]!.Value<byte>().Should().Be(chain.AddressVersion);
        protocol["MillisecondsPerBlock"]!.Value<uint>().Should().Be(3000);
        protocol["MaxTransactionsPerBlock"]!.Value<uint>().Should().Be(1024);
        protocol["MemoryPoolMaxTransactions"]!.Value<int>().Should().Be(60000);
        protocol["MaxTraceableBlocks"]!.Value<uint>().Should().Be(1000);
        protocol["MaxValidUntilBlockIncrement"]!.Value<uint>().Should().Be(100);
        protocol["InitialGasDistribution"]!.Value<ulong>().Should().Be(123456789);
        protocol["ValidatorsCount"]!.Value<int>().Should().Be(1);
        protocol["StandbyCommittee"]!.Should().HaveCount(1);
        protocol["SeedList"]!.Values<string>().Should().ContainSingle().Which.Should().EndWith(":50013");
        protocol["Hardforks"]![nameof(Hardfork.HF_Faun)]!.Value<uint>().Should().Be(42);
        protocol["Hardforks"]![nameof(Hardfork.HF_Gorgon)]!.Value<uint>().Should().Be(42);
        protocol["Hardforks"]![nameof(Hardfork.HF_Huyao)]!.Value<uint>().Should().Be(84);

        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(textWriter.ToString()));
            var loaded = ProtocolSettings.Load(stream);
            loaded.Network.Should().Be(chain.Network);
            loaded.MillisecondsPerBlock.Should().Be(3000);
            loaded.MaxTransactionsPerBlock.Should().Be(1024);
            loaded.MemoryPoolMaxTransactions.Should().Be(60000);
            loaded.MaxTraceableBlocks.Should().Be(1000);
            loaded.MaxValidUntilBlockIncrement.Should().Be(100);
            loaded.InitialGasDistribution.Should().Be(123456789);
            loaded.Hardforks[Hardfork.HF_Faun].Should().Be(42);
            loaded.Hardforks[Hardfork.HF_Gorgon].Should().Be(42);
            loaded.Hardforks[Hardfork.HF_Huyao].Should().Be(84);
        }
        finally
        {
            ProtocolSettings.Custom = null;
        }
    }
}
