using Newtonsoft.Json;
using System;

namespace NeoExpress.Models
{
    class UnspentTransaction
    {
        [JsonProperty("txid")]
        public string TransactionId { get; set; } = string.Empty;

        [JsonProperty("n")]
        public ushort Index { get; set; }

        [JsonProperty("value")]
        public decimal Value { get; set; }
    }
}
