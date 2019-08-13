using Newtonsoft.Json;

namespace Neo.Express.Abstractions
{
    public class ExpressConsensusNode
    {
        [JsonProperty("tcp-port")]
        public long TcpPort { get; set; }

        [JsonProperty("ws-port")]
        public long WebSocketPort { get; set; }

        [JsonProperty("rpc-port")]
        public long RpcPort { get; set; }

        [JsonProperty("wallet")]
        public ExpressWallet Wallet { get; set; }
    }
}
