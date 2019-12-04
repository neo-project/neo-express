using Newtonsoft.Json;
using System;

namespace NeoExpress.Models
{

    public class UnspentsResponse
    {
        [JsonProperty("address")]
        public string Address { get; set; } = string.Empty;

        [JsonProperty("balance")]
        public UnspentBalance[] Balance { get; set; } = Array.Empty<UnspentBalance>();
    }
}
