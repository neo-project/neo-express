// Copyright (C) 2015-2026 The Neo Project.
//
// AdditionalGasTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using NeoExpress.Node;
using System;
using Xunit;

namespace test.workflowvalidation;

public class AdditionalGasTests
{
    [Fact]
    public void AdditionalGasSystemFee_converts_a_fractional_gas_value()
    {
        // 1.5 GAS -> 1.5 * 10^8 datoshi.
        NodeUtility.AdditionalGasSystemFee(1.5m).Should().Be(150_000_000L);
    }

    [Fact]
    public void SystemFeeDelta_does_not_pad_ordinary_transactions()
    {
        NodeUtility.SystemFeeDelta(0m).Should().Be(0L);
        NodeUtility.SystemFeeDelta(1m).Should().Be(100_000_000L);
        NodeUtility.SystemFeeDelta(1m, padInvokeEstimate: false).Should().Be(100_000_000L);
    }

    [Fact]
    public void SystemFeeDelta_pads_only_when_requested_for_deploy_or_update()
    {
        NodeUtility.SystemFeeDelta(0m, padInvokeEstimate: true).Should().Be(NodeUtility.InvokeEstimatePadDatoshi);
        NodeUtility.SystemFeeDelta(1m, padInvokeEstimate: true).Should().Be(100_000_000L + NodeUtility.InvokeEstimatePadDatoshi);
    }

    [Fact]
    public void SystemFeeDelta_rejects_a_padded_fee_that_would_overflow()
    {
        // 92233720368.54775807 GAS scales to exactly long.MaxValue datoshi, which the
        // converter accepts; adding the deploy/update estimate pad to it used to wrap
        // into a negative system fee.
        var maxDatoshiGas = 92233720368.54775807m;

        NodeUtility.SystemFeeDelta(maxDatoshiGas).Should().Be(long.MaxValue);
        var act = () => NodeUtility.SystemFeeDelta(maxDatoshiGas, padInvokeEstimate: true);

        act.Should().Throw<Exception>().WithMessage("*too large*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AdditionalGasSystemFee_returns_zero_for_non_positive_values(decimal value)
    {
        NodeUtility.AdditionalGasSystemFee(value).Should().Be(0L);
    }

    [Fact]
    public void AdditionalGasSystemFee_rejects_more_than_eight_decimal_places()
    {
        var act = () => NodeUtility.AdditionalGasSystemFee(0.123456789m);

        act.Should().Throw<Exception>().WithMessage("*decimal places*");
    }

    [Fact]
    public void AdditionalGasSystemFee_rejects_a_value_that_overflows_the_fee()
    {
        // 100,000,000,000 GAS * 10^8 exceeds Int64.MaxValue.
        var act = () => NodeUtility.AdditionalGasSystemFee(100_000_000_000m);

        act.Should().Throw<Exception>().WithMessage("*too large*");
    }
}
