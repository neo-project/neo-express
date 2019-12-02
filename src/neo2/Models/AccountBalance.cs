using Newtonsoft.Json;
using System;

namespace Neo2Express.Models
{
    public class AccountBalance
    {
        [JsonProperty("asset")]
        public string Asset { get; set; } = string.Empty;

        [JsonProperty("value")]
        public decimal Value { get; set; }
    }
}
