// Copyright (C) 2015-2026 The Neo Project.
//
// Nep24CompliantErrorsTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.SmartContract.Manifest;
using NeoExpress;
using Xunit;

namespace test.workflowvalidation;

public class Nep24CompliantErrorsTests
{
    const string ManifestJson = @"{
        ""name"": ""Test"",
        ""groups"": [],
        ""features"": {},
        ""supportedstandards"": [""NEP-24""],
        ""abi"": {
            ""methods"": [
                {
                    ""name"": ""royaltyInfo"",
                    ""parameters"": [
                        { ""name"": ""tokenId"", ""type"": ""ByteArray"" },
                        { ""name"": ""royaltyToken"", ""type"": ""Hash160"" },
                        { ""name"": ""salePrice"", ""type"": ""Integer"" }
                    ],
                    ""returntype"": ""Array"",
                    ""offset"": 0,
                    ""safe"": true
                }
            ],
            ""events"": []
        },
        ""permissions"": [{ ""contract"": ""*"", ""methods"": ""*"" }],
        ""trusts"": [],
        ""extra"": null
    }";

    [Fact]
    public void Compliant_royaltyInfo_produces_no_errors()
    {
        var manifest = ContractManifest.Parse(ManifestJson);

        manifest.Nep24CompliantErrors().Should().BeEmpty();
    }
}
