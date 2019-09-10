using Newtonsoft.Json;

namespace NeoExpress.Models
{
    public class ExpressConsensusNode
    {
        [JsonProperty("wallet")]
        public ExpressWallet Wallet { get; set; }
    }
}
