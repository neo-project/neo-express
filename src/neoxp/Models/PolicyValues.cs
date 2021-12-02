using Neo;

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
    }
}
