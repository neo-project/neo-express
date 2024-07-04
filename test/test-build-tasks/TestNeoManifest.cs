// Copyright (C) 2015-2024 The Neo Project.
//
// TestNeoManifest.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using Xunit;

namespace build_tasks
{
    public class TestNeoManifest
    {
        [Fact]
        public void parse_sample_manifest()
        {
            var json = SimpleJSON.JSON.Parse(MANIFEST) ?? throw new InvalidOperationException();
            var manifest = Neo.BuildTasks.NeoManifest.FromManifestJson(json);
            Assert.Equal("DevHawk.Contracts.ApocToken", manifest.Name);
            Assert.Equal(13, manifest.Methods.Count);
            Assert.Single(manifest.Events);
        }

        const string MANIFEST = @"{
    ""groups"": [],
    ""abi"": {
        ""methods"": [
            {
                ""name"": ""_initialize"",
                ""offset"": ""0"",
                ""safe"": false,
                ""parameters"": [],
                ""returntype"": ""Void""
            },
            {
                ""name"": ""balanceOf"",
                ""offset"": ""95"",
                ""safe"": false,
                ""parameters"": [
                    {
                        ""name"": ""account"",
                        ""type"": ""Hash160""
                    }
                ],
                ""returntype"": ""Integer""
            },
            {
                ""name"": ""decimals"",
                ""offset"": ""213"",
                ""safe"": false,
                ""parameters"": [],
                ""returntype"": ""Integer""
            },
            {
                ""name"": ""deploy"",
                ""offset"": ""236"",
                ""safe"": false,
                ""parameters"": [
                    {
                        ""name"": ""update"",
                        ""type"": ""Boolean""
                    }
                ],
                ""returntype"": ""Void""
            },
            {
                ""name"": ""destroy"",
                ""offset"": ""455"",
                ""safe"": false,
                ""parameters"": [],
                ""returntype"": ""Void""
            },
            {
                ""name"": ""disablePayment"",
                ""offset"": ""578"",
                ""safe"": false,
                ""parameters"": [],
                ""returntype"": ""Void""
            },
            {
                ""name"": ""enablePayment"",
                ""offset"": ""667"",
                ""safe"": false,
                ""parameters"": [],
                ""returntype"": ""Void""
            },
            {
                ""name"": ""onPayment"",
                ""offset"": ""1245"",
                ""safe"": false,
                ""parameters"": [
                    {
                        ""name"": ""from"",
                        ""type"": ""Hash160""
                    },
                    {
                        ""name"": ""amount"",
                        ""type"": ""Integer""
                    },
                    {
                        ""name"": ""data"",
                        ""type"": ""Any""
                    }
                ],
                ""returntype"": ""Void""
            },
            {
                ""name"": ""symbol"",
                ""offset"": ""1705"",
                ""safe"": false,
                ""parameters"": [],
                ""returntype"": ""String""
            },
            {
                ""name"": ""totalSupply"",
                ""offset"": ""1712"",
                ""safe"": false,
                ""parameters"": [],
                ""returntype"": ""Integer""
            },
            {
                ""name"": ""transfer"",
                ""offset"": ""1719"",
                ""safe"": false,
                ""parameters"": [
                    {
                        ""name"": ""from"",
                        ""type"": ""Hash160""
                    },
                    {
                        ""name"": ""to"",
                        ""type"": ""Hash160""
                    },
                    {
                        ""name"": ""amount"",
                        ""type"": ""Integer""
                    },
                    {
                        ""name"": ""data"",
                        ""type"": ""Any""
                    }
                ],
                ""returntype"": ""Boolean""
            },
            {
                ""name"": ""update"",
                ""offset"": ""2098"",
                ""safe"": false,
                ""parameters"": [
                    {
                        ""name"": ""nefFile"",
                        ""type"": ""ByteArray""
                    },
                    {
                        ""name"": ""manifest"",
                        ""type"": ""String""
                    }
                ],
                ""returntype"": ""Void""
            },
            {
                ""name"": ""verify"",
                ""offset"": ""2214"",
                ""safe"": false,
                ""parameters"": [],
                ""returntype"": ""Boolean""
            }
        ],
        ""events"": [
            {
                ""name"": ""Transfer"",
                ""parameters"": [
                    {
                        ""name"": ""arg1"",
                        ""type"": ""Hash160""
                    },
                    {
                        ""name"": ""arg2"",
                        ""type"": ""Hash160""
                    },
                    {
                        ""name"": ""arg3"",
                        ""type"": ""Integer""
                    }
                ]
            }
        ]
    },
    ""permissions"": [
        {
            ""contract"": ""*"",
            ""methods"": ""*""
        }
    ],
    ""trusts"": [],
    ""name"": ""DevHawk.Contracts.ApocToken"",
    ""supportedstandards"": [
        ""NEP17"",
        ""NEP10""
    ],
    ""extra"": {
        ""Author"": ""Harry Pierson"",
        ""Email"": ""harrypierson@hotmail.com"",
        ""Description"": ""This is a NEP17 example""
    }
}";
    }
}
