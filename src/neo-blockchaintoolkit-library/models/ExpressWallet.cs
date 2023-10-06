// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Neo.BlockchainToolkit.Models
{
    public class ExpressWallet
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("accounts")]
        public List<ExpressWalletAccount> Accounts { get; set; } = new List<ExpressWalletAccount>();

        [JsonIgnore]
        public ExpressWalletAccount DefaultAccount => Accounts.SingleOrDefault(a => a.IsDefault);
    }
}
