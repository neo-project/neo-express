// Copyright (C) 2015-2026 The Neo Project.
//
// BranchInfoHardforkTests.cs file belongs to neo-express project and is free
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
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace test.bctklib
{
    public class BranchInfoHardforkTests
    {
        [Fact]
        public void Parse_preserves_hardforks_in_protocol_settings()
        {
            var json = CreateBranchJson();
            json["hardforks"] = new JObject
            {
                ["HF_Echidna"] = 123u,
                ["HF_Gorgon"] = 456u,
            };

            var branchInfo = BranchInfo.Parse(json);

            branchInfo.Hardforks.Should().NotBeNull();
            branchInfo.ProtocolSettings.Hardforks[Hardfork.HF_Basilisk]
                .Should().Be(ProtocolSettings.Default.Hardforks[Hardfork.HF_Basilisk]);
            branchInfo.ProtocolSettings.Hardforks[Hardfork.HF_Echidna].Should().Be(123u);
            branchInfo.ProtocolSettings.Hardforks[Hardfork.HF_Gorgon].Should().Be(456u);
        }

        [Fact]
        public void Parse_uses_default_hardforks_when_metadata_is_missing()
        {
            var branchInfo = BranchInfo.Parse(CreateBranchJson());

            branchInfo.Hardforks.Should().BeNull();
            branchInfo.ProtocolSettings.Hardforks.Should().BeEquivalentTo(ProtocolSettings.Default.Hardforks);
        }

        [Fact]
        public void WriteJson_round_trips_hardfork_metadata()
        {
            var hardforks = new Dictionary<Hardfork, uint>
            {
                [Hardfork.HF_Echidna] = 123u,
                [Hardfork.HF_Gorgon] = 456u,
            };
            var branchInfo = new BranchInfo(
                5195086,
                ProtocolSettings.Default.AddressVersion,
                100,
                UInt256.Zero,
                UInt256.Zero,
                new List<ContractInfo>(),
                hardforks);

            var json = WriteJson(branchInfo);
            var parsed = BranchInfo.Parse(json);

            json["hardforks"].Should().NotBeNull();
            parsed.ProtocolSettings.Hardforks[Hardfork.HF_Echidna].Should().Be(123u);
            parsed.ProtocolSettings.Hardforks[Hardfork.HF_Gorgon].Should().Be(456u);
        }

        static JObject CreateBranchJson()
        {
            return new JObject
            {
                ["network"] = 5195086,
                ["address-version"] = ProtocolSettings.Default.AddressVersion,
                ["index"] = 100,
                ["index-hash"] = $"{UInt256.Zero}",
                ["root-hash"] = $"{UInt256.Zero}",
                ["contracts"] = new JArray(),
            };
        }

        static JObject WriteJson(BranchInfo branchInfo)
        {
            using var writer = new StringWriter();
            using var jsonWriter = new JsonTextWriter(writer);
            branchInfo.WriteJson(jsonWriter);
            jsonWriter.Flush();
            return JObject.Parse(writer.ToString());
        }
    }
}
