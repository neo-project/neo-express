using Newtonsoft.Json;
using System;

namespace NeoExpress.Models
{
    class AccountResponse
    {
        [JsonProperty("version")]
        public long Version { get; set; }

        [JsonProperty("script_hash")]
        public string ScriptHash { get; set; } = string.Empty;

        [JsonProperty("frozen")]
        public bool Frozen { get; set; }

        [JsonProperty("balances")]
        public AccountBalance[] Balances { get; set; } = Array.Empty<AccountBalance>();
    }
}
