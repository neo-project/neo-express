// Copyright (C) 2015-2026 The Neo Project.
//
// ContractGetCommandTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.SmartContract.Manifest;
using NeoExpress.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace test.workflowvalidation;

public class ContractGetCommandTests
{
    [Fact]
    public void WriteContracts_writes_complete_json_array()
    {
        var manifest = ContractManifest.Parse("""
        {
          "name":"SampleContract",
          "groups":[],
          "features":{},
          "supportedstandards":[],
          "abi":{
            "methods":[{"name":"dummy","parameters":[],"returntype":"Void","offset":0,"safe":false}],
            "events":[]
          },
          "permissions":[],
          "trusts":[],
          "extra":{}
        }
        """);
        var hash = UInt160.Parse("0x0101010101010101010101010101010101010101");
        using var stringWriter = new StringWriter();
        using var jsonWriter = new JsonTextWriter(stringWriter);

        ContractCommand.Get.WriteContracts(jsonWriter, [(hash, manifest)]);

        var json = JArray.Parse(stringWriter.ToString());
        json.Should().HaveCount(1);
        json[0]!.Value<string>("name").Should().Be("SampleContract");
        json[0]!.Value<string>("hash").Should().Be(hash.ToString());
    }
}
