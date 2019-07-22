using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;

namespace Neo.Express
{
    public class DevChain
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

        public uint Magic { get; set; }
        public List<DevConsensusNode> ConsensusNodes { get; set; }
        public List<DevWallet> Wallets { get; set; }

        public DevChain(uint magic, IEnumerable<DevConsensusNode> consensusNodes, IEnumerable<DevWallet> wallets = null)
        {
            Magic = magic;
            ConsensusNodes = consensusNodes.ToList();
            Wallets = wallets == null ? new List<DevWallet>() : wallets.ToList();
        }

        public DevChain(IEnumerable<DevConsensusNode> consensusNodes, IEnumerable<DevWallet> wallets = null)
            : this(GenerateMagicValue(), consensusNodes, wallets)
        {
        }

        public static DevChain FromJson(JObject json)
        {
            var magic = json.Value<uint>("magic");
            var consensusNodes = json["consensus-nodes"].Select(DevConsensusNode.FromJson);
            var wallets = json["wallets"].Select(DevWallet.FromJson);
            return new DevChain(magic, consensusNodes, wallets);
        }

        public void ToJson(JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("magic");
            writer.WriteValue(Magic);
            writer.WritePropertyName("consensus-nodes");
            writer.WriteStartArray();
            foreach (var conensusNode in ConsensusNodes)
            {
                conensusNode.ToJson(writer);
            }
            writer.WriteEndArray();
            writer.WritePropertyName("wallets");
            writer.WriteStartArray();
            foreach (var wallet in Wallets)
            {
                wallet.ToJson(writer);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        public static DevChain Load(string filename)
        {
            using (var stream = File.OpenRead(filename))
            using (var reader = new JsonTextReader(new StreamReader(stream)))
            {
                var json = JObject.Load(reader);
                return FromJson(json);
            }
        }

        public void Save(string filename)
        {
            using (var stream = File.Open(filename, FileMode.Create, FileAccess.Write))
            using (var writer = new JsonTextWriter(new StreamWriter(stream)) { Formatting = Formatting.Indented })
            {
                ToJson(writer);
            }
        }

        // InitializeProtocolSettings uses the dev chain's raw JSON information 
        // to avoid default initialization of ProtocolSettings.
        public static bool InitializeProtocolSettings(JObject json, uint secondsPerBlock = 0)
        {
            var magic = json.Value<uint>("magic");
            var nodes = json["consensus-nodes"].Select(DevConsensusNode.ProtocolSettingsFromJson);
            secondsPerBlock = secondsPerBlock == 0 ? 15 : secondsPerBlock;

            IEnumerable<KeyValuePair<string, string>> settings()
            {
                yield return new KeyValuePair<string, string>(
                    "ProtocolConfiguration:Magic", $"{magic}");
                yield return new KeyValuePair<string, string>(
                    "ProtocolConfiguration:AddressVersion", $"{(byte)0x17}");
                yield return new KeyValuePair<string, string>(
                    "ProtocolConfiguration:SecondsPerBlock", $"{secondsPerBlock}");

                foreach (var node in nodes.Select((n, i) => (config: n, index: i)))
                {
                    yield return new KeyValuePair<string, string>(
                        $"ProtocolConfiguration:StandbyValidators:{node.index}", node.config.publicKey.EncodePoint(true).ToHexString());
                    yield return new KeyValuePair<string, string>(
                        $"ProtocolConfiguration:SeedList:{node.index}", $"{IPAddress.Loopback}:{node.config.tcpPort}");
                }
            }

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings())
                .Build();

            return ProtocolSettings.Initialize(config);
        }
    }
}
