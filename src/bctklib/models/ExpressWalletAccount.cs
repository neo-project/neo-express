// Copyright (C) 2015-2024 The Neo Project.
//
// ExpressWalletAccount.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Newtonsoft.Json;

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
        public AccountContract? Contract { get; set; }
    }
}
