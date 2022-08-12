using System.Numerics;
using Neo;
using Neo.Json;
using Neo.SmartContract.Native;

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
            json[nameof(StorageFeeFactor)] = StorageFeeFactor;
            json[nameof(ExecutionFeeFactor)] = ExecutionFeeFactor;
            return json;
        }

        public static PolicyValues FromJson(JObject json)
        {
            var gasPerBlock = ParseGasValue(json[nameof(GasPerBlock)]!);
            var minimumDeploymentFee = ParseGasValue(json[nameof(MinimumDeploymentFee)]!);
            var candidateRegistrationFee = ParseGasValue(json[nameof(CandidateRegistrationFee)]!);
            var oracleRequestFee = ParseGasValue(json[nameof(OracleRequestFee)]!);
            var networkFeePerByte = ParseGasValue(json[nameof(NetworkFeePerByte)]!);
            var storageFeeFactor = (uint)json[nameof(NetworkFeePerByte)]!.AsNumber();
            var executionFeeFactor = (uint)json[nameof(ExecutionFeeFactor)]!.AsNumber();

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

            static BigDecimal ParseGasValue(JToken json) => new BigDecimal(BigInteger.Parse(json.AsString()), NativeContract.GAS.Decimals);
        }
    }
}
