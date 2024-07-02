// Copyright (C) 2015-2024 The Neo Project.
//
// ExpressConsensusNode.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Newtonsoft.Json;

namespace Neo.BlockchainToolkit.Models
{
    public class ExpressConsensusNode
    {
        [JsonProperty("tcp-port")]
        public ushort TcpPort { get; set; }

        [JsonProperty("rpc-port")]
        public ushort RpcPort { get; set; }

        [JsonProperty("debug-port", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public ushort DebugPort { get; set; }

        [JsonProperty("wallet")]
        public ExpressWallet Wallet { get; set; } = new ExpressWallet();
    }
}
