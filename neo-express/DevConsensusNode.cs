using Neo.Cryptography.ECC;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.Express
{
    class DevConsensusNode
    {
        public DevWallet Wallet { get; set; }
        public ushort TcpPort { get; set; }
        public ushort WebSocketPort { get; set; }
        public ushort RpcPort { get; set; }

        public static DevConsensusNode FromJson(JToken json)
        {
            var (tcpPort, webSocketPort, rpcPort) = PortsFromJson(json);
            var wallet = DevWallet.FromJson(json["wallet"]);
            return new DevConsensusNode()
            {
                Wallet = wallet,
                TcpPort = tcpPort,
                WebSocketPort = webSocketPort,
                RpcPort = rpcPort
            };
        }

        public static (ushort tcpPort, ushort webSocketPort, ushort rpcPort) PortsFromJson(JToken json)
        {
            var tcpPort = json.Value<ushort>("tcp-port");
            var wsPort = json.Value<ushort>("ws-port");
            var rpcPort = json.Value<ushort>("rpc-port");
            return (tcpPort, wsPort, rpcPort);
        }

        public static (ECPoint publicKey, ushort tcpPort) ProtocolSettingsFromJson(JToken json)
        {
            var keyPair = DevWallet.KeyPairFromJson(json["wallet"]);
            var (tcp, _, _) = PortsFromJson(json);

            return (keyPair.PublicKey, tcp);
        }

        public void ToJson(JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("tcp-port");
            writer.WriteValue(TcpPort);
            writer.WritePropertyName("ws-port");
            writer.WriteValue(WebSocketPort);
            writer.WritePropertyName("rpc-port");
            writer.WriteValue(RpcPort);
            writer.WritePropertyName("wallet");
            Wallet.ToJson(writer);
            writer.WriteEndObject();
        }
    }
}
