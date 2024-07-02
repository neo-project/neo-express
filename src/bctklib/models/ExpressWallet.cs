// Copyright (C) 2015-2024 The Neo Project.
//
// ExpressWallet.cs file belongs to neo-express project and is free
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
    public class ExpressWallet
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("accounts")]
        public List<ExpressWalletAccount> Accounts { get; set; } = new List<ExpressWalletAccount>();

        [JsonIgnore]
        public ExpressWalletAccount? DefaultAccount => Accounts.SingleOrDefault(a => a.IsDefault);
    }
}
