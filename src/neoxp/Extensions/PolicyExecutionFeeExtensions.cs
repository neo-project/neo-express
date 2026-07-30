// Copyright (C) 2015-2026 The Neo Project.
//
// PolicyExecutionFeeExtensions.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.SmartContract;
using NeoExpress.Models;

namespace NeoExpress
{
    internal static class PolicyExecutionFeeExtensions
    {
        internal static ulong GetScaledExecFeeFactorArgument(uint logicalFactor, bool isFaunEnabled)
        {
            var feeFactor = (ulong)ApplicationEngine.FeeFactor;
            return isFaunEnabled
                ? checked((ulong)logicalFactor * feeFactor)
                : (ulong)logicalFactor;
        }

        internal static ulong GetScaledExecFeeFactorArgument(this PolicyValues policy, bool isFaunEnabled) =>
            GetScaledExecFeeFactorArgument(policy.ExecutionFeeFactor, isFaunEnabled);
    }
}
