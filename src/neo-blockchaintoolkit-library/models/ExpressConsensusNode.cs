// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Newtonsoft.Json;

namespace Neo.BlockchainToolkit.Models
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
