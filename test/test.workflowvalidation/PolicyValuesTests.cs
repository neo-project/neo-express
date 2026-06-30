// Copyright (C) 2015-2026 The Neo Project.
//
// PolicyValuesTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.Json;
using NeoExpress.Models;
using System;
using Xunit;

namespace test.workflowvalidation;

public class PolicyValuesTests
{
    [Fact]
    public void FromJson_names_a_missing_required_value()
    {
        // Valid JSON, but the ExecutionFeeFactor key is absent. This must be reported as a
        // missing value rather than dereferencing null and throwing a NullReferenceException.
        var json = (JObject)JObject.Parse(
            "{\"GasPerBlock\":\"500000000\",\"MinimumDeploymentFee\":\"1000000000\","
            + "\"CandidateRegistrationFee\":\"100000000000\",\"OracleRequestFee\":\"50000000\","
            + "\"NetworkFeePerByte\":\"1000\",\"StorageFeeFactor\":100000}")!;

        var act = () => PolicyValues.FromJson(json);

        act.Should().Throw<FormatException>().WithMessage("*ExecutionFeeFactor*");
    }

    [Fact]
    public void FromJson_round_trips_a_complete_policy()
    {
        var original = new PolicyValues
        {
            GasPerBlock = new Neo.BigDecimal((System.Numerics.BigInteger)500000000, 8),
            MinimumDeploymentFee = new Neo.BigDecimal((System.Numerics.BigInteger)1000000000, 8),
            CandidateRegistrationFee = new Neo.BigDecimal((System.Numerics.BigInteger)100000000000, 8),
            OracleRequestFee = new Neo.BigDecimal((System.Numerics.BigInteger)50000000, 8),
            NetworkFeePerByte = new Neo.BigDecimal((System.Numerics.BigInteger)1000, 8),
            StorageFeeFactor = 100000,
            ExecutionFeeFactor = 30,
        };

        var result = PolicyValues.FromJson(original.ToJson());

        result.StorageFeeFactor.Should().Be(original.StorageFeeFactor);
        result.ExecutionFeeFactor.Should().Be(original.ExecutionFeeFactor);
        result.GasPerBlock.Value.Should().Be(original.GasPerBlock.Value);
    }
}
