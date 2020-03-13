using Newtonsoft.Json;

namespace NeoExpress.Neo2.Models
{
    public class ExpressConsensusNode
    {
        [JsonProperty("tcp-port")]
        public ushort TcpPort { get; set; }

        [JsonProperty("ws-port")]
        public ushort WebSocketPort { get; set; }

        [JsonProperty("rpc-port")]
        public ushort RpcPort { get; set; }

        [JsonProperty("debug-port", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public ushort DebugPort { get; set; }

        [JsonProperty("wallet")]
        public ExpressWallet Wallet { get; set; } = new ExpressWallet();
    }
}
