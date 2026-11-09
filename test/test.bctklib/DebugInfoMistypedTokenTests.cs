// Copyright (C) 2015-2026 The Neo Project.
//
// DebugInfoMistypedTokenTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BlockchainToolkit.Models;
using Newtonsoft.Json.Linq;
using System;
using Xunit;

namespace test.bctklib
{
    // Debug-info JSON fields that are expected to be strings must be rejected
    // with a clean FormatException when the token is a JSON array/object,
    // rather than escaping a raw InvalidCastException from JToken.Value<string>().
    public class DebugInfoMistypedTokenTests
    {
        const string TEST_HASH = "0xf69e5188632deb3a9273519efc86cb68da8d42b8";

        [Fact]
        public void cant_parse_hash_as_array()
        {
            var json = new JObject
            {
                ["hash"] = new JArray(),
            };

            Assert.Throws<FormatException>(() => DebugInfo.Parse(json));
        }

        [Fact]
        public void cant_parse_hash_as_object()
        {
            var json = new JObject
            {
                ["hash"] = new JObject(),
            };

            Assert.Throws<FormatException>(() => DebugInfo.Parse(json));
        }

        [Fact]
        public void cant_parse_documentroot_as_array()
        {
            var json = new JObject
            {
                ["hash"] = TEST_HASH,
                ["document-root"] = new JArray(),
            };

            Assert.Throws<FormatException>(() => DebugInfo.Parse(json));
        }

        [Fact]
        public void cant_parse_documents_element_as_object()
        {
            var json = new JObject
            {
                ["hash"] = TEST_HASH,
                ["documents"] = new JArray(new JObject()),
            };

            Assert.Throws<FormatException>(() => DebugInfo.Parse(json));
        }

        [Fact]
        public void cant_parse_documents_element_as_array()
        {
            var json = new JObject
            {
                ["hash"] = TEST_HASH,
                ["documents"] = new JArray { new JArray() },
            };

            Assert.Throws<FormatException>(() => DebugInfo.Parse(json));
        }

        [Fact]
        public void cant_parse_event_params_element_as_object()
        {
            var json = new JObject
            {
                ["hash"] = TEST_HASH,
                ["events"] = new JArray(new JObject
                {
                    ["id"] = "7",
                    ["name"] = "Neo.Express,OnTransfer",
                    ["params"] = new JArray(new JObject()),
                }),
            };

            Assert.Throws<FormatException>(() => DebugInfo.Parse(json));
        }

        [Fact]
        public void cant_parse_static_variables_element_as_object()
        {
            var json = new JObject
            {
                ["hash"] = TEST_HASH,
                ["static-variables"] = new JArray(new JObject()),
            };

            Assert.Throws<FormatException>(() => DebugInfo.Parse(json));
        }

        [Fact]
        public void cant_parse_method_variables_element_as_array()
        {
            var json = new JObject
            {
                ["hash"] = TEST_HASH,
                ["methods"] = new JArray(new JObject
                {
                    ["id"] = "0",
                    ["name"] = "Neo.Express,Main",
                    ["range"] = "0-1",
                    ["variables"] = new JArray { new JArray() },
                }),
            };

            Assert.Throws<FormatException>(() => DebugInfo.Parse(json));
        }

        [Fact]
        public void cant_parse_event_id_as_array()
        {
            var json = new JObject
            {
                ["hash"] = TEST_HASH,
                ["events"] = new JArray(new JObject
                {
                    ["id"] = new JArray(),
                    ["name"] = "Neo.Express,OnTransfer",
                }),
            };

            Assert.Throws<FormatException>(() => DebugInfo.Parse(json));
        }

        [Fact]
        public void cant_parse_method_id_as_array()
        {
            var json = new JObject
            {
                ["hash"] = TEST_HASH,
                ["methods"] = new JArray(new JObject
                {
                    ["id"] = new JArray(),
                    ["name"] = "Neo.Express,Main",
                    ["range"] = "0-1",
                }),
            };

            Assert.Throws<FormatException>(() => DebugInfo.Parse(json));
        }

        [Fact]
        public void cant_parse_method_return_as_array()
        {
            var json = new JObject
            {
                ["hash"] = TEST_HASH,
                ["methods"] = new JArray(new JObject
                {
                    ["id"] = "0",
                    ["name"] = "Neo.Express,Main",
                    ["range"] = "0-1",
                    ["return"] = new JArray(),
                }),
            };

            Assert.Throws<FormatException>(() => DebugInfo.Parse(json));
        }

        [Fact]
        public void cant_parse_method_range_as_array()
        {
            var json = new JObject
            {
                ["hash"] = TEST_HASH,
                ["methods"] = new JArray(new JObject
                {
                    ["id"] = "0",
                    ["name"] = "Neo.Express,Main",
                    ["range"] = new JArray("0-1"),
                }),
            };

            Assert.Throws<FormatException>(() => DebugInfo.Parse(json));
        }

        [Fact]
        public void cant_parse_method_range_as_object()
        {
            var json = new JObject
            {
                ["hash"] = TEST_HASH,
                ["methods"] = new JArray(new JObject
                {
                    ["id"] = "0",
                    ["name"] = "Neo.Express,Main",
                    ["range"] = new JObject(),
                }),
            };

            Assert.Throws<FormatException>(() => DebugInfo.Parse(json));
        }

        [Fact]
        public void cant_parse_sequence_point_as_object()
        {
            var json = new JObject
            {
                ["hash"] = TEST_HASH,
                ["methods"] = new JArray(new JObject
                {
                    ["id"] = "0",
                    ["name"] = "Neo.Express,Main",
                    ["range"] = "0-1",
                    ["sequence-points"] = new JArray(new JObject()),
                }),
            };

            Assert.Throws<FormatException>(() => DebugInfo.Parse(json));
        }

        [Fact]
        public void cant_parse_method_name_as_array()
        {
            var json = new JObject
            {
                ["hash"] = TEST_HASH,
                ["methods"] = new JArray(new JObject
                {
                    ["id"] = "0",
                    ["name"] = new JArray(),
                    ["range"] = "0-1",
                }),
            };

            Assert.Throws<FormatException>(() => DebugInfo.Parse(json));
        }

        [Fact]
        public void cant_parse_event_name_as_array()
        {
            var json = new JObject
            {
                ["hash"] = TEST_HASH,
                ["events"] = new JArray(new JObject
                {
                    ["id"] = "7",
                    ["name"] = new JArray(),
                }),
            };

            Assert.Throws<FormatException>(() => DebugInfo.Parse(json));
        }

        // Numeric fields beyond Int32 range must be rejected with a clean
        // FormatException rather than a raw OverflowException from int.Parse.
        [Fact]
        public void cant_parse_sequence_point_address_overflow()
        {
            var json = new JObject
            {
                ["hash"] = TEST_HASH,
                ["methods"] = new JArray(new JObject
                {
                    ["id"] = "0",
                    ["name"] = "Neo.Express,Main",
                    ["range"] = "0-1",
                    ["sequence-points"] = new JArray("99999999999999999999[1]10:5-10:20"),
                }),
            };

            Assert.Throws<FormatException>(() => DebugInfo.Parse(json));
        }

        [Fact]
        public void cant_parse_sequence_point_end_column_overflow()
        {
            var json = new JObject
            {
                ["hash"] = TEST_HASH,
                ["methods"] = new JArray(new JObject
                {
                    ["id"] = "0",
                    ["name"] = "Neo.Express,Main",
                    ["range"] = "0-1",
                    ["sequence-points"] = new JArray("10[1]10:5-10:99999999999999999999"),
                }),
            };

            Assert.Throws<FormatException>(() => DebugInfo.Parse(json));
        }

        [Fact]
        public void cant_parse_method_range_start_overflow()
        {
            var json = new JObject
            {
                ["hash"] = TEST_HASH,
                ["methods"] = new JArray(new JObject
                {
                    ["id"] = "0",
                    ["name"] = "Neo.Express,Main",
                    ["range"] = "99999999999999999999-1",
                }),
            };

            Assert.Throws<FormatException>(() => DebugInfo.Parse(json));
        }

        [Fact]
        public void cant_parse_method_range_end_overflow()
        {
            var json = new JObject
            {
                ["hash"] = TEST_HASH,
                ["methods"] = new JArray(new JObject
                {
                    ["id"] = "0",
                    ["name"] = "Neo.Express,Main",
                    ["range"] = "0-99999999999999999999",
                }),
            };

            Assert.Throws<FormatException>(() => DebugInfo.Parse(json));
        }
    }
}
