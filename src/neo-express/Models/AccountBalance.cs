using Newtonsoft.Json;
using System;

namespace NeoExpress.Models
{
    public class AccountBalance
    {
        [JsonProperty("asset")]
        public string Asset { get; set; } = string.Empty;

        [JsonProperty("value")]
        public decimal Value { get; set; }
    }
}
