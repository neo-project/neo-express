// Copyright (C) 2015-2026 The Neo Project.
//
// PolicyExecutionFeeScalingTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using NeoExpress;
using NeoExpress.Models;
using System.Collections.Immutable;
using System.Numerics;
using Xunit;

namespace test.workflowvalidation;

public class PolicyExecutionFeeScalingTests
{
    [Theory]
    [InlineData(1u, false, 1u)]
    [InlineData(30u, false, 30u)]
    [InlineData(100u, false, 100u)]
    [InlineData(1u, true, 10_000u)]
    [InlineData(30u, true, 300_000u)]
    [InlineData(100u, true, 1_000_000u)]
    public void GetScaledExecFeeFactorArgument_uint_matches_expected_storage_units(uint logical, bool faun, ulong expected)
    {
        PolicyExecutionFeeExtensions.GetScaledExecFeeFactorArgument(logical, faun).Should().Be(expected);
        logical.Should().BeLessOrEqualTo(100u, "Neo Policy MaxExecFeeFactor is 100 for the logical factor");
        if (faun)
            expected.Should().Be((ulong)logical * ApplicationEngine.FeeFactor);
    }

    [Fact]
    public void GetScaledExecFeeFactorArgument_on_PolicyValues_delegates_to_uint_overload()
    {
        var policy = CreatePolicyValues(executionFeeFactor: 42);
        PolicyExecutionFeeExtensions.GetScaledExecFeeFactorArgument(policy, isFaunEnabled: false).Should().Be(42u);
        PolicyExecutionFeeExtensions.GetScaledExecFeeFactorArgument(policy, isFaunEnabled: true)
            .Should().Be(42u * ApplicationEngine.FeeFactor);
    }

    /// <summary>
    /// Documents the same rule as <c>TransactionExecutor.IsFaunHardforkEnabledForNextTxAsync</c>:
    /// use the block after the chain tip when deciding whether the upcoming tx runs under Faun.
    /// </summary>
    [Theory]
    [InlineData(0u, 0u, true)]
    [InlineData(0u, 1u, true)]
    [InlineData(100u, 99u, false)]
    [InlineData(100u, 100u, true)]
    [InlineData(100u, 101u, true)]
    public void IsHardforkEnabled_Faun_uses_block_index_like_next_tx_check(uint faunActivationHeight, uint indexToCheck, bool expectedEnabled)
    {
        var settings = ProtocolSettingsWithFaunAt(faunActivationHeight);
        settings.IsHardforkEnabled(Hardfork.HF_Faun, indexToCheck).Should().Be(expectedEnabled);
    }

    [Fact]
    public void Next_block_after_tip_uses_tip_index_plus_one()
    {
        const uint tipIndex = 99;
        var settings = ProtocolSettingsWithFaunAt(100);
        settings.IsHardforkEnabled(Hardfork.HF_Faun, tipIndex).Should().BeFalse();
        settings.IsHardforkEnabled(Hardfork.HF_Faun, tipIndex + 1).Should().BeTrue();
    }

    static ProtocolSettings ProtocolSettingsWithFaunAt(uint faunHeight)
    {
        var baseForks = ProtocolSettings.Default.Hardforks;
        var builder = ImmutableDictionary.CreateBuilder<Hardfork, uint>();
        foreach (var pair in baseForks)
            builder.Add(pair.Key, pair.Key == Hardfork.HF_Faun ? faunHeight : pair.Value);
        if (!builder.ContainsKey(Hardfork.HF_Faun))
            builder.Add(Hardfork.HF_Faun, faunHeight);

        return ProtocolSettings.Default with { Hardforks = builder.ToImmutable() };
    }

    static PolicyValues CreatePolicyValues(uint executionFeeFactor, uint storageFeeFactor = 1)
    {
        var zero = new BigDecimal(BigInteger.Zero, NativeContract.GAS.Decimals);
        return new PolicyValues
        {
            GasPerBlock = zero,
            MinimumDeploymentFee = zero,
            CandidateRegistrationFee = zero,
            OracleRequestFee = zero,
            NetworkFeePerByte = zero,
            StorageFeeFactor = storageFeeFactor,
            ExecutionFeeFactor = executionFeeFactor,
        };
    }
}
