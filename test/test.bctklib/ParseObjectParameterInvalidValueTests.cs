// Copyright (C) 2015-2026 The Neo Project.
//
// ParseObjectParameterInvalidValueTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit;
using Neo.SmartContract;
using Newtonsoft.Json.Linq;
using System;
using Xunit;

namespace test.bctklib
{
    // Regression tests for a fuzzing-discovered crash class in
    // ContractParameterParser.ParseObjectParameter (repro artifacts:
    // artifacts/fuzz/crashes-round10/param-1-min.json, param-2-min.json and
    // param-3-min.json). A value token whose shape doesn't match the declared
    // parameter type (an object or array for an Integer/Boolean/Hash160
    // parameter, or null for Boolean) made Newtonsoft's Value<T> conversion
    // throw a raw InvalidCastException instead of the FormatException the same
    // method already uses for malformed values ("invalid parameter type").
    public class ParseObjectParameterInvalidValueTests
    {
        readonly ContractParameterParser parser = new(ProtocolSettings.Default.AddressVersion);

        ContractParameter Parse(string json) => parser.ParseParameter(JToken.Parse(json));

        // param-1-min.json: {"type":"Integer","value":{...}}
        [Fact]
        public void integer_parameter_with_object_value_reports_format_error()
        {
            var json = new JObject
            {
                ["type"] = "Integer",
                ["value"] = new JObject { ["x"] = 1 },
            };

            var action = () => parser.ParseObjectParameter(json);

            action.Should().Throw<FormatException>()
                .WithMessage("*invalid Integer parameter value*");
        }

        // A non-string "type" field made json.Value<string>("type") throw a raw
        // InvalidCastException before the type was even parsed.
        [Fact]
        public void object_type_field_reports_format_error()
        {
            var json = new JObject
            {
                ["type"] = new JObject { ["bogus"] = true },
                ["value"] = 1,
            };

            var action = () => parser.ParseObjectParameter(json);

            action.Should().Throw<FormatException>()
                .WithMessage("*invalid parameter type*");
        }

        [Fact]
        public void array_type_field_reports_format_error()
        {
            var json = new JObject
            {
                ["type"] = new JArray(1, 2),
                ["value"] = 1,
            };

            var action = () => parser.ParseObjectParameter(json);

            action.Should().Throw<FormatException>()
                .WithMessage("*invalid parameter type*");
        }

        // param-2-min.json: {"type":"Hash160","value":[...]}
        [Fact]
        public void hash160_parameter_with_array_value_reports_format_error()
        {
            var json = new JObject
            {
                ["type"] = "Hash160",
                ["value"] = new JArray(1, 2),
            };

            var action = () => parser.ParseObjectParameter(json);

            action.Should().Throw<FormatException>()
                .WithMessage("*invalid Hash160 parameter value*");
        }

        // param-3-min.json: {"type":"Boolean","value":null} — Convert.ChangeType
        // threw InvalidCastException ("Null object cannot be converted to a value type")
        [Fact]
        public void boolean_parameter_with_null_value_reports_format_error()
        {
            var json = new JObject
            {
                ["type"] = "Boolean",
                ["value"] = JValue.CreateNull(),
            };

            var action = () => parser.ParseObjectParameter(json);

            action.Should().Throw<FormatException>()
                .WithMessage("*invalid Boolean parameter value*");
        }

        [Fact]
        public void hash256_parameter_with_object_value_reports_format_error()
        {
            var json = new JObject
            {
                ["type"] = "Hash256",
                ["value"] = new JObject(),
            };

            var action = () => parser.ParseObjectParameter(json);

            action.Should().Throw<FormatException>()
                .WithMessage("*invalid Hash256 parameter value*");
        }

        // A String parameter with a null value threw an empty-message JsonException;
        // it must keep throwing JsonException but with a useful message.
        [Fact]
        public void string_parameter_with_null_value_reports_json_error_with_message()
        {
            var json = new JObject
            {
                ["type"] = "String",
                ["value"] = JValue.CreateNull(),
            };

            var action = () => parser.ParseObjectParameter(json);

            action.Should().Throw<Newtonsoft.Json.JsonException>()
                .WithMessage("invalid String parameter value");
        }

        // Well-formed typed parameters of the same shapes keep working.
        [Fact]
        public void well_formed_typed_parameters_still_parse()
        {
            Parse("{\"type\":\"Integer\",\"value\":\"42\"}").Value.Should().Be(new System.Numerics.BigInteger(42));
            Parse("{\"type\":\"Boolean\",\"value\":true}").Value.Should().Be(true);
            Parse("{\"type\":\"String\",\"value\":\"hello\"}").Value.Should().Be("hello");
            Parse("{\"type\":\"Hash160\",\"value\":\"0000000000000000000000000000000000000001\"}")
                .Value.Should().Be(UInt160.Parse("0000000000000000000000000000000000000001"));
        }
    }
}
