using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Neo.Express.Abstractions
{
    public class ExpressWallet
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("accounts")]
        public List<ExpressWalletAccount> Accounts { get; set; }

        [JsonIgnore]
        public ExpressWalletAccount DefaultAccount => Accounts
            .SingleOrDefault(a => a.IsDefault);

    }
}
