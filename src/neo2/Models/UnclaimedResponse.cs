using Newtonsoft.Json;
using System;

namespace NeoExpress.Neo2.Models
{
    public class UnclaimedResponse
    {
        [JsonProperty("unavailable")]
        public decimal Unavailable { get; set; }

        [JsonProperty("available")]
        public decimal Available { get; set; }

        [JsonProperty("unclaimed")]
        public decimal Unclaimed { get; set; }
    }
}
