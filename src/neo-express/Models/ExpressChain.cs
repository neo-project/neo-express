using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Newtonsoft.Json;

#nullable enable

namespace NeoExpress.Models
{
    public class ExpressChain
    {
        private readonly static ImmutableArray<uint> KNOWN_MAGIC_NUMBERS = ImmutableArray.Create(
            /* NEO 2 MainNet */ 7630401u,
            /* NEO 2 TestNet */ 1953787457u,
            /* NEO 3 Preview 1 MainNet */ 5195086u,
            /* NEO 3 Preview 1 TestNet */ 1951352142u);

        public static uint GenerateMagicValue()
        {
            var random = new Random();

            while (true)
            {
                uint magic = (uint)random.Next(int.MaxValue);

                if (!KNOWN_MAGIC_NUMBERS.Contains(magic))
                {
                    return magic;
                }
            }
        }

        [JsonProperty("magic")]
        public long Magic { get; set; }

        [JsonProperty("consensus-nodes")]
        public List<ExpressConsensusNode> ConsensusNodes { get; set; } = new List<ExpressConsensusNode>();

        [JsonProperty("wallets")]
        public List<ExpressWallet> Wallets { get; set; } = new List<ExpressWallet>();

        [JsonProperty("contracts")]
        public List<ExpressContract> Contracts { get; set; } = new List<ExpressContract>();
    }
}
