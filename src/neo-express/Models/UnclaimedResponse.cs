using Newtonsoft.Json;
using System;

namespace NeoExpress.Models
{
    class UnclaimedResponse
    {
        [JsonProperty("unavailable")]
        public decimal Unavailable { get; set; }

        [JsonProperty("available")]
        public decimal Available { get; set; }

        [JsonProperty("unclaimed")]
        public decimal Unclaimed { get; set; }
    }
}
