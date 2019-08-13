using System.Collections.Generic;
using Newtonsoft.Json;

namespace Neo.Express.Abstractions
{
    public class ExpressWalletAccount
    {
        public class AccountContract
        {
            [JsonProperty("script")]
            public string Script { get; set; }

            [JsonProperty("parameters")]
            public List<string> Parameters { get; set; }
        }

        [JsonProperty("private-key")]
        public string PrivateKey { get; set; }

        [JsonProperty("script-hash")]
        public string ScriptHash { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("is-default")]
        public bool IsDefault { get; set; }

        [JsonProperty("contract")]
        public AccountContract Contract { get; set; }
    }
}
