// Copyright (C) 2015-2026 The Neo Project.
//
// PolicyValues.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.Json;
using Neo.SmartContract.Native;
using NeoExpress.Utility;
using System.Numerics;

namespace NeoExpress.Models
{
    class PolicyValues
    {
        public BigDecimal GasPerBlock { get; init; }
        public BigDecimal MinimumDeploymentFee { get; init; }
        public BigDecimal CandidateRegistrationFee { get; init; }
        public BigDecimal OracleRequestFee { get; init; }
        public BigDecimal NetworkFeePerByte { get; init; }
        public uint StorageFeeFactor { get; init; }
        public uint ExecutionFeeFactor { get; init; }

        public JObject ToJson()
        {
            var decimals = Neo.SmartContract.Native.NativeContract.GAS.Decimals;

            var json = new JObject();
            json[nameof(GasPerBlock)] = $"{GasPerBlock.ChangeDecimals(decimals).Value}";
            json[nameof(MinimumDeploymentFee)] = $"{MinimumDeploymentFee.ChangeDecimals(decimals).Value}";
            json[nameof(CandidateRegistrationFee)] = $"{CandidateRegistrationFee.ChangeDecimals(decimals).Value}";
            json[nameof(OracleRequestFee)] = $"{OracleRequestFee.ChangeDecimals(decimals).Value}";
            json[nameof(NetworkFeePerByte)] = $"{NetworkFeePerByte.ChangeDecimals(decimals).Value}";
            json[nameof(StorageFeeFactor)] = (long)StorageFeeFactor;
            json[nameof(ExecutionFeeFactor)] = (long)ExecutionFeeFactor;
            return json;
        }

        public static PolicyValues FromJson(JObject json)
        {
            var gasPerBlock = ParseGasValue(Required(nameof(GasPerBlock)));
            var minimumDeploymentFee = ParseGasValue(Required(nameof(MinimumDeploymentFee)));
            var candidateRegistrationFee = ParseGasValue(Required(nameof(CandidateRegistrationFee)));
            var oracleRequestFee = ParseGasValue(Required(nameof(OracleRequestFee)));
            var networkFeePerByte = ParseGasValue(Required(nameof(NetworkFeePerByte)));
            var storageFeeFactor = SafeCast.ToUInt32((BigInteger)Required(nameof(StorageFeeFactor)).AsNumber());
            var executionFeeFactor = SafeCast.ToUInt32((BigInteger)Required(nameof(ExecutionFeeFactor)).AsNumber());

            return new PolicyValues
            {
                GasPerBlock = gasPerBlock,
                MinimumDeploymentFee = minimumDeploymentFee,
                CandidateRegistrationFee = candidateRegistrationFee,
                OracleRequestFee = oracleRequestFee,
                NetworkFeePerByte = networkFeePerByte,
                StorageFeeFactor = storageFeeFactor,
                ExecutionFeeFactor = executionFeeFactor,
            };

            // A missing key would otherwise dereference null and throw an opaque
            // NullReferenceException; name the absent value instead.
            JToken Required(string name) => json[name]
                ?? throw new FormatException($"policy is missing required value \"{name}\"");

            static BigDecimal ParseGasValue(JToken json) => new BigDecimal(BigInteger.Parse(json.AsString()), NativeContract.GAS.Decimals);
        }
    }
}
