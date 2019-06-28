using Neo.Cryptography.ECC;
using Neo.Wallets;
using System;
using System.Text.Json;

namespace Neo.Express
{
    class DevConensusNode
    {
        public DevWallet Wallet { get; set; }
        public ushort TcpPort { get; set; }
        public ushort WebSocketPort { get; set; }
        public ushort RpcPort { get; set; }

        static (ushort tcpPort, ushort webSocketPort, ushort rpcPort) ParsePorts(JsonElement json)
        {
            return ((ushort)json.GetProperty("tcp-port").GetUInt32(),
                (ushort)json.GetProperty("ws-port").GetUInt32(),
                (ushort)json.GetProperty("rpc-port").GetUInt32());

        }

        public static DevConensusNode Parse(JsonElement json)
        {
            var (tcp, ws, rpc) = ParsePorts(json);
            return new DevConensusNode()
            {
                Wallet = DevWallet.Parse(json.GetProperty("wallet")),
                TcpPort = tcp,
                WebSocketPort = ws,
                RpcPort = rpc,
            };
        }

        public static (ECPoint publicKey, ushort tcpPort) ParseProtocolSettings(JsonElement json)
        {
            var keyPair = DevWallet.ParseKeyPair(json.GetProperty("wallet"));
            var (tcp, _, _) = ParsePorts(json);

            return (keyPair.PublicKey, tcp);
        }

        public void Write(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WriteNumber("tcp-port", TcpPort);
            writer.WriteNumber("ws-port", WebSocketPort);
            writer.WriteNumber("rpc-port", RpcPort);
            Wallet.Write(writer, "wallet");
            writer.WriteEndObject();
        }
    }
}
