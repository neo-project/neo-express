using Newtonsoft.Json;
using System;

namespace NeoExpress.Neo2.Models
{
    class AccountBalance
    {
        [JsonProperty("asset")]
        public string Asset { get; set; } = string.Empty;

        [JsonProperty("value")]
        public decimal Value { get; set; }
    }
}
