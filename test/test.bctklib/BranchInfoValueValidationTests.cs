// Copyright (C) 2015-2026 The Neo Project.
//
// BranchInfoValueValidationTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit.Models;
using Newtonsoft.Json.Linq;
using System;
using Xunit;

namespace test.bctklib;

// Regression tests for fuzzing-discovered crash classes in the
// BranchInfo.Parse numeric/string token reads (used by the worknet file
// load path in NeoWorkNet Utility.LoadWorknetAsync and WorkNetChain.Parse).
// Wrong-typed tokens threw raw InvalidCastException ("Object must implement
// IConvertible") and out-of-range digits threw OverflowException from
// unguarded JToken.Value<T> conversions; both must surface as clean
// FormatException validation errors naming the offending field.
public class BranchInfoValueValidationTests
{
    static BranchInfo ParseBranchJson(string json) => BranchInfo.Parse(JObject.Parse(json));

    [Theory]
    [InlineData("9999999999991")] // digit-bombed index, OverflowException
    [InlineData("{}")]            // wrong-typed token, InvalidCastException
    public void invalid_index_reports_clean_validation_error(string valueJson)
    {
        var action = () => ParseBranchJson($"{{'network': 5195086, 'address-version': 53, 'index': {valueJson}, 'index-hash': '{UInt256.Zero}', 'root-hash': '{UInt256.Zero}', 'contracts': []}}");

        action.Should().Throw<FormatException>().WithMessage("*index*");
    }

    [Theory]
    [InlineData("9999999999991")]
    [InlineData("[]")]
    public void invalid_network_reports_clean_validation_error(string valueJson)
    {
        var action = () => ParseBranchJson($"{{'network': {valueJson}, 'address-version': 53, 'index': 100, 'index-hash': '{UInt256.Zero}', 'root-hash': '{UInt256.Zero}', 'contracts': []}}");

        action.Should().Throw<FormatException>().WithMessage("*network*");
    }

    [Theory]
    [InlineData("999")] // byte overflow
    [InlineData("{}")]
    public void invalid_address_version_reports_clean_validation_error(string valueJson)
    {
        var action = () => ParseBranchJson($"{{'network': 5195086, 'address-version': {valueJson}, 'index': 100, 'index-hash': '{UInt256.Zero}', 'root-hash': '{UInt256.Zero}', 'contracts': []}}");

        action.Should().Throw<FormatException>().WithMessage("*address-version*");
    }

    [Theory]
    [InlineData("99999999999999999999")] // BigInteger token, InvalidCastException
    [InlineData("{}")]
    public void invalid_contract_id_reports_clean_validation_error(string valueJson)
    {
        var action = () => ParseBranchJson($"{{'network': 5195086, 'address-version': 53, 'index': 100, 'index-hash': '{UInt256.Zero}', 'root-hash': '{UInt256.Zero}', 'contracts': [{{'id': {valueJson}, 'hash': '{UInt160.Zero}', 'name': 'contract-1'}}]}}");

        action.Should().Throw<FormatException>().WithMessage("*id*");
    }

    [Theory]
    [InlineData("{}")]
    public void invalid_contract_hash_reports_clean_validation_error(string valueJson)
    {
        var action = () => ParseBranchJson($"{{'network': 5195086, 'address-version': 53, 'index': 100, 'index-hash': '{UInt256.Zero}', 'root-hash': '{UInt256.Zero}', 'contracts': [{{'id': 1, 'hash': {valueJson}, 'name': 'contract-1'}}]}}");

        action.Should().Throw<FormatException>().WithMessage("*hash*");
    }

    [Theory]
    [InlineData("{}")]
    public void invalid_contract_name_reports_clean_validation_error(string valueJson)
    {
        var action = () => ParseBranchJson($"{{'network': 5195086, 'address-version': 53, 'index': 100, 'index-hash': '{UInt256.Zero}', 'root-hash': '{UInt256.Zero}', 'contracts': [{{'id': 1, 'hash': '{UInt160.Zero}', 'name': {valueJson}}}]}}");

        action.Should().Throw<FormatException>().WithMessage("*name*");
    }

    [Theory]
    [InlineData("9999999999999999990")] // out-of-range hardfork height, OverflowException
    [InlineData("{}")]                  // wrong-typed token, InvalidCastException
    public void invalid_hardfork_value_reports_clean_validation_error(string valueJson)
    {
        var action = () => ParseBranchJson($"{{'network': 5195086, 'address-version': 53, 'index': 100, 'index-hash': '{UInt256.Zero}', 'root-hash': '{UInt256.Zero}', 'contracts': [], 'hardforks': {{'HF_Echidna': {valueJson}}}}}");

        action.Should().Throw<FormatException>().WithMessage("*HF_Echidna*");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("99999999999999999999")] // huge number token is not string-convertible
    public void invalid_index_hash_reports_clean_validation_error(string valueJson)
    {
        var action = () => ParseBranchJson($"{{'network': 5195086, 'address-version': 53, 'index': 100, 'index-hash': {valueJson}, 'root-hash': '{UInt256.Zero}', 'contracts': []}}");

        action.Should().Throw<FormatException>().WithMessage("*index-hash*");
    }

    [Theory]
    [InlineData("{}")]
    public void invalid_root_hash_reports_clean_validation_error(string valueJson)
    {
        var action = () => ParseBranchJson($"{{'network': 5195086, 'address-version': 53, 'index': 100, 'index-hash': '{UInt256.Zero}', 'root-hash': {valueJson}, 'contracts': []}}");

        action.Should().Throw<FormatException>().WithMessage("*root-hash*");
    }

    [Fact]
    public void numeric_string_values_keep_converting()
    {
        // pre-existing leniency: string-encoded numbers must keep working
        var branchInfo = ParseBranchJson($"{{'network': '5195086', 'address-version': '53', 'index': '100', 'index-hash': '{UInt256.Zero}', 'root-hash': '{UInt256.Zero}', 'contracts': []}}");

        branchInfo.Network.Should().Be(5195086u);
        branchInfo.AddressVersion.Should().Be((byte)53);
        branchInfo.Index.Should().Be(100u);
    }
}
