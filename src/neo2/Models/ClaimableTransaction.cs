using Newtonsoft.Json;
using System;

namespace NeoExpress.Neo2.Models
{
    class ClaimableTransaction
    {
        [JsonProperty("txid")]
        public string TransactionId { get; set; } = string.Empty;

        [JsonProperty("n")]
        public ushort Index { get; set; }

        [JsonProperty("value")]
        public decimal Value { get; set; }

        [JsonProperty("start_height")]
        public uint StartHeight { get; set; }

        [JsonProperty("end_height")]
        public uint EndHeight { get; set; }

        [JsonProperty("generated")]
        public decimal Generated { get; set; }

        [JsonProperty("sys_fee")]
        public decimal SystemFee { get; set; }

        [JsonProperty("unclaimed")]
        public decimal Unclaimed { get; set; }
    }
}
