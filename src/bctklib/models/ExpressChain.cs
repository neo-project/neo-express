// Copyright (C) 2015-2024 The Neo Project.
//
// ExpressChain.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Newtonsoft.Json;
using System.Collections.Immutable;

namespace Neo.BlockchainToolkit.Models
{
    public class ExpressChain
    {
        private readonly static ImmutableHashSet<uint> KNOWN_NETWORK_NUMBERS = ImmutableHashSet.Create<uint>(
            /* Neo 2 MainNet */ 7630401,
            /* Neo 2 TestNet */ 1953787457,
            /* Neo 3 MainNet */ 860833102,
            /* Neo 3 T5 TestNet */ 894710606,
            /* Neo 3 T4 TestNet */ 877933390,
            /* Neo 3 RC3 TestNet */ 844378958,
            /* Neo 3 RC1 TestNet */ 827601742,
            /* Neo 3 Preview5 TestNet */ 894448462);

        public static uint GenerateNetworkValue()
        {
            var random = new Random();
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            while (true)
            {
                random.NextBytes(buffer);
                uint network = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(buffer);

                if (network > 0 && !KNOWN_NETWORK_NUMBERS.Contains(network))
                {
                    return network;
                }
            }
        }

        [JsonProperty("magic")]
        public uint Network { get; set; }

        [JsonProperty("address-version")]
        public byte AddressVersion { get; set; } = ProtocolSettings.Default.AddressVersion;

        [JsonProperty("consensus-nodes")]
        public List<ExpressConsensusNode> ConsensusNodes { get; set; } = new List<ExpressConsensusNode>();

        [JsonProperty("wallets")]
        public List<ExpressWallet> Wallets { get; set; } = new List<ExpressWallet>();

        [JsonProperty("settings")]
        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();
    }
}
