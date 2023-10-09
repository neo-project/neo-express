// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Newtonsoft.Json;
using System.Collections.Generic;

namespace Neo.BlockchainToolkit.Models
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
        public AccountContract Contract { get; set; }
    }
}
