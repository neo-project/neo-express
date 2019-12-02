using Newtonsoft.Json;
using System;

namespace Neo2Express.Models
{
    public class UnspentTransaction
    {
        [JsonProperty("txid")]
        public string TransactionId { get; set; } = string.Empty;

        [JsonProperty("n")]
        public long Index { get; set; }

        [JsonProperty("value")]
        public decimal Value { get; set; }
    }
}
