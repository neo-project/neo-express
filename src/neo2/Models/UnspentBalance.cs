using Newtonsoft.Json;
using System;

namespace NeoExpress.Neo2.Models
{
    class UnspentBalance
    {
        [JsonProperty("asset_hash")]
        public string AssetHash { get; set; } = string.Empty;

        [JsonProperty("asset")]
        public string Asset { get; set; } = string.Empty;

        [JsonProperty("asset_symbol")]
        public string AssetSymbol { get; set; } = string.Empty;

        [JsonProperty("amount")]
        public decimal Amount { get; set; }

        [JsonProperty("unspent")]
        public UnspentTransaction[] Transactions { get; set; } = Array.Empty<UnspentTransaction>();
    }
}
