using Newtonsoft.Json;

namespace NeoExpress.Models
{
    public class ExpressConsensusNode
    {
        [JsonProperty("tcp-port")]
        public ushort TcpPort { get; set; }

        [JsonProperty("ws-port")]
        public ushort WebSocketPort { get; set; }

        [JsonProperty("rpc-port")]
        public ushort RpcPort { get; set; }

        [JsonProperty("debug-port")]
        public ushort DebugPort { get; set; }

        [JsonProperty("wallet")]
        public ExpressWallet Wallet { get; set; } = new ExpressWallet();
    }
}
