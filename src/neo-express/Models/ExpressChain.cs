using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace NeoExpress.Models
{
    public class ExpressChain
    {
        private readonly static ImmutableArray<uint> KNOWN_MAGIC_NUMBERS = ImmutableArray.Create(
            /* NEO 3 MainNet */ 0x4F454Eu,
            /* NEO 2 TestNet */ 0x544F454Eu,
            /* NEO 2 MainNet */ 0x746E41u);

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
        public List<ExpressConsensusNode> ConsensusNodes { get; set; }

        [JsonProperty("wallets")]
        public List<ExpressWallet> Wallets { get; set; }

        [JsonProperty("contracts")]
        public List<ExpressContract> Contracts { get; set; }
    }
}
