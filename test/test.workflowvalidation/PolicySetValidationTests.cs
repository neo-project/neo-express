// Copyright (C) 2015-2026 The Neo Project.
//
// PolicySetValidationTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using NeoExpress;
using NeoExpress.Models;
using System;
using System.Numerics;
using Xunit;

namespace test.workflowvalidation
{
    public class PolicySetValidationTests
    {
        internal static PolicyValues MakeValues(
            Neo.BigDecimal? gasPerBlock = null,
            Neo.BigDecimal? minimumDeploymentFee = null,
            Neo.BigDecimal? candidateRegistrationFee = null,
            Neo.BigDecimal? oracleRequestFee = null,
            Neo.BigDecimal? networkFeePerByte = null,
            uint? storageFeeFactor = null,
            uint? executionFeeFactor = null) => new()
            {
                GasPerBlock = gasPerBlock ?? new Neo.BigDecimal((System.Numerics.BigInteger)500000000, 8),
                MinimumDeploymentFee = minimumDeploymentFee ?? new Neo.BigDecimal((System.Numerics.BigInteger)100000000, 8),
                CandidateRegistrationFee = candidateRegistrationFee ?? new Neo.BigDecimal((System.Numerics.BigInteger)100000000, 8),
                OracleRequestFee = oracleRequestFee ?? new Neo.BigDecimal((System.Numerics.BigInteger)100000000, 8),
                NetworkFeePerByte = networkFeePerByte ?? new Neo.BigDecimal((System.Numerics.BigInteger)100000, 8),
                StorageFeeFactor = storageFeeFactor ?? 1000,
                ExecutionFeeFactor = executionFeeFactor ?? 30,
            };

        [Fact]
        public void values_within_the_native_bounds_are_accepted()
        {
            var act = () => TransactionExecutor.ValidatePolicyValues(MakeValues());
            act.Should().NotThrow();
        }

        [Theory]
        [InlineData(0, false)]   // native setRegisterPrice requires a positive value
        [InlineData(-1, false)]
        [InlineData(1, true)]
        public void candidate_registration_fee_must_be_positive(int gas, bool expectedValid)
        {
            var act = () => TransactionExecutor.ValidatePolicyValues(
                MakeValues(candidateRegistrationFee: new Neo.BigDecimal((System.Numerics.BigInteger)gas, 0)));
            if (expectedValid)
                act.Should().NotThrow();
            else
                act.Should().Throw<InvalidOperationException>();
        }

        [Theory]
        [InlineData(0, false)]   // native setStoragePrice requires [1, MaxStoragePrice]
        [InlineData(1, true)]
        [InlineData(10000000, true)]
        [InlineData(10000001, false)]
        [InlineData(-1, false)]
        public void storage_price_bounds_match_the_native_contract(int value, bool expectedValid)
        {
            var uintValue = unchecked((uint)value);
            var act = () => TransactionExecutor.ValidateStoragePrice(uintValue);
            if (expectedValid)
                act.Should().NotThrow();
            else
                act.Should().Throw<InvalidOperationException>();
        }

        [Theory]
        [InlineData(0, false)]   // native setExecFeeFactor requires [1, MaxExecFeeFactor]
        [InlineData(1, true)]
        [InlineData(100, true)]
        [InlineData(101, false)]
        [InlineData(-1, false)]
        public void exec_fee_factor_bounds_match_the_native_contract(int value, bool expectedValid)
        {
            var uintValue = unchecked((uint)value);
            var act = () => TransactionExecutor.ValidateExecFeeFactor(uintValue);
            if (expectedValid)
                act.Should().NotThrow();
            else
                act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void gas_policy_bounds_match_the_native_contract()
        {
            // max GasPerBlock is 10 GAS
            var inRange = () => TransactionExecutor.ValidateGasPolicySetting(PolicySettings.GasPerBlock, 10 * NativeContract.GAS.Factor);
            inRange.Should().NotThrow();
            var overMax = () => TransactionExecutor.ValidateGasPolicySetting(PolicySettings.GasPerBlock, 10 * NativeContract.GAS.Factor + 1);
            overMax.Should().Throw<InvalidOperationException>();
            var negative = () => TransactionExecutor.ValidateGasPolicySetting(PolicySettings.GasPerBlock, -1);
            negative.Should().Throw<InvalidOperationException>();

            // FeePerByte caps at 100000000 datoshi
            var feeAtCap = () => TransactionExecutor.ValidateGasPolicySetting(PolicySettings.NetworkFeePerByte, TransactionExecutor.MaxFeePerByte);
            feeAtCap.Should().NotThrow();
            var feeOverCap = () => TransactionExecutor.ValidateGasPolicySetting(PolicySettings.NetworkFeePerByte, TransactionExecutor.MaxFeePerByte + 1);
            feeOverCap.Should().Throw<InvalidOperationException>();

            // deployment fee only requires non-negative
            var zeroFee = () => TransactionExecutor.ValidateGasPolicySetting(PolicySettings.MinimumDeploymentFee, BigInteger.Zero);
            zeroFee.Should().NotThrow();
            var negativeFee = () => TransactionExecutor.ValidateGasPolicySetting(PolicySettings.MinimumDeploymentFee, -1);
            negativeFee.Should().Throw<InvalidOperationException>();
        }
    }
}
