using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Newtonsoft.Json;

namespace NeoExpress.Abstractions.Models
{
    public class ExpressChain
    {
        private readonly static ImmutableArray<uint> KNOWN_MAGIC_NUMBERS = ImmutableArray.Create(
            /* Neo 2 MainNet */ 7630401u,
            /* Neo 2 TestNet */ 1953787457u,
            /* Neo 3 Preview 1 MainNet */ 5195086u,
            /* Neo 3 Preview 1 TestNet */ 1951352142u);

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

        public static ExpressChain Load(string filename)
        {
            var serializer = new JsonSerializer();
            using var stream = File.OpenRead(filename);
            using var reader = new JsonTextReader(new StreamReader(stream));
            return serializer.Deserialize<ExpressChain>(reader)
                ?? throw new Exception($"Cannot load Neo-Express instance information from {filename}");
        }

        public void Save(string fileName)
        {
            var serializer = new JsonSerializer();
            using (var stream = File.Open(fileName, FileMode.Create, FileAccess.Write))
            using (var writer = new JsonTextWriter(new StreamWriter(stream)) { Formatting = Formatting.Indented })
            {
                serializer.Serialize(writer, this);
            }
        }

        public const byte AddressVersion = (byte)0x17;

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
