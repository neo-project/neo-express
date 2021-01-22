using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace NeoExpress.Abstractions.Models
{
    public class ExpressWallet
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("accounts")]
        public List<ExpressWalletAccount> Accounts { get; set; } = new List<ExpressWalletAccount>();

        [JsonIgnore]
        public ExpressWalletAccount DefaultAccount => Accounts
            .SingleOrDefault(a => a.IsDefault);
    }
}
