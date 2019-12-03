using Newtonsoft.Json;
using System;

namespace Neo2Express.Models
{
    public class ClaimableTransaction
    {
        [JsonProperty("txid")]
        public string TransactionId { get; set; } = string.Empty;

        [JsonProperty("n")]
        public long Index { get; set; }

        [JsonProperty("value")]
        public decimal Value { get; set; }

        [JsonProperty("start_height")]
        public long StartHeight { get; set; }

        [JsonProperty("end_height")]
        public long EndHeight { get; set; }

        [JsonProperty("generated")]
        public double Generated { get; set; }

        [JsonProperty("sys_fee")]
        public double SystemFee { get; set; }

        [JsonProperty("unclaimed")]
        public double Unclaimed { get; set; }
    }
}
