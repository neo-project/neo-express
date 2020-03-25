using System.Collections.Generic;
using Newtonsoft.Json;

namespace NeoExpress.Abstractions.Models
{
    public class ExpressWalletAccount
    {
        public class AccountContract
        {
            [JsonProperty("script")]
            public string Script { get; set; } = string.Empty;

            [JsonProperty("parameters")]
            public List<string> Parameters { get; set; } = new List<string>();
        }

        [JsonProperty("private-key")]
        public string PrivateKey { get; set; } = string.Empty;

        [JsonProperty("script-hash")]
        public string ScriptHash { get; set; } = string.Empty;

        [JsonProperty("label")]
        public string Label { get; set; } = string.Empty;

        [JsonProperty("is-default")]
        public bool IsDefault { get; set; }

        [JsonProperty("contract")]
        public AccountContract Contract { get; set; } = new AccountContract();
    }
}
