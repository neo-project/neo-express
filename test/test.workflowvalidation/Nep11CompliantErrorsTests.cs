// Copyright (C) 2015-2026 The Neo Project.
//
// Nep11CompliantErrorsTests.cs file belongs to neo-express project and is free
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

public class Nep11CompliantErrorsTests
{
    // A divisible NEP-11 token exposes the two-parameter balanceOf(owner, tokenId)
    // with owner: Hash160 and tokenId: ByteArray.
    const string ManifestJson = @"{
        ""name"": ""Test"",
        ""groups"": [],
        ""features"": {},
        ""supportedstandards"": [""NEP-11""],
        ""abi"": {
            ""methods"": [
                {
                    ""name"": ""balanceOf"",
                    ""parameters"": [
                        { ""name"": ""owner"", ""type"": ""Hash160"" },
                        { ""name"": ""tokenId"", ""type"": ""ByteArray"" }
                    ],
                    ""returntype"": ""Integer"",
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
    public void Divisible_balanceOf_is_accepted()
    {
        var manifest = ContractManifest.Parse(ManifestJson);

        manifest.Nep11CompliantErrors()
            .Should().NotContain("Incomplete or unsafe NEP standard NEP-11 implementation: balanceOf");
    }
}
