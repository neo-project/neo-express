﻿using Newtonsoft.Json;
using System;

namespace NeoExpress.Neo2.Models
{
    class ClaimableResponse
    {
        [JsonProperty("claimable")]
        public ClaimableTransaction[] Transactions { get; set; } = Array.Empty<ClaimableTransaction>();

        [JsonProperty("address")]
        public string Address { get; set; } = string.Empty;

        [JsonProperty("unclaimed")]
        public decimal Unclaimed { get; set; }
    }
}
