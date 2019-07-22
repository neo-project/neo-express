using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;

namespace Neo.Express
{
    class DevChain
    {
        readonly static ImmutableArray<uint> KNOWN_MAGIC_NUMBERS = ImmutableArray.Create(
            /* NEO 3 MainNet */ 0x4F454Eu,
            /* NEO 2 TestNet */ 0x544F454Eu,
            /* NEO 2 MainNet */ 0x746E41u);

        static uint GenerateMagicValue()
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
        public uint Magic { get; set; }

        [JsonProperty("consensus-nodes")]
        public List<DevConsensusNode> ConsensusNodes { get; set; }

        [JsonProperty("wallets")]
        [JsonConverter(typeof(DevWalletListConverter))]
        public List<DevWallet> Wallets { get; set; }

        [JsonConstructor]
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

        //public static DevChain Parse(JsonElement json)
        //{
        //    return new DevChain(
        //        json.GetProperty("magic").GetUInt32(),
        //        json.GetProperty("consensus-nodes").EnumerateArray().Select(DevConsensusNode.Parse),
        //        json.GetProperty("wallets").EnumerateArray().Select(DevWallet.Parse));
        //}

        //public static DevChain Parse(JsonDocument doc)
        //{
        //    return Parse(doc.RootElement);
        //}

        public static DevChain Load(string filename)
        {
            using (var stream = File.OpenRead(filename))
            using (var jsonReader = new JsonTextReader(new StreamReader(stream)))
            {
                var ser = new JsonSerializer();
                return ser.Deserialize<DevChain>(jsonReader);
            }
        }

        public void Save(string filename)
        {
            using (var stream = File.Open(filename, FileMode.Create, FileAccess.Write))
            using (var jsonWriter = new JsonTextWriter(new StreamWriter(stream)))
            {
                JsonSerializer ser = new JsonSerializer()
                {
                    Formatting = Formatting.Indented
                };
                ser.Serialize(jsonWriter, this);
            }
        }

        // InitializeProtocolSettings uses the dev chain's raw JSON information 
        // to avoid default initialization of ProtocolSettings.
        //public static bool InitializeProtocolSettings(JsonElement json, uint secondsPerBlock = 0)
        //{
        //    var nodes = json.GetProperty("consensus-nodes")
        //        .EnumerateArray()
        //        .Select(DevConsensusNode.ParseProtocolSettings);

        //    secondsPerBlock = secondsPerBlock == 0 ? 15 : secondsPerBlock;

        //    IEnumerable<KeyValuePair<string, string>> settings()
        //    {
        //        yield return new KeyValuePair<string, string>(
        //            "ProtocolConfiguration:Magic", $"{json.GetProperty("magic").GetUInt32()}");
        //        yield return new KeyValuePair<string, string>(
        //            "ProtocolConfiguration:AddressVersion", $"{(byte)0x17}");
        //        yield return new KeyValuePair<string, string>(
        //            "ProtocolConfiguration:SecondsPerBlock", $"{secondsPerBlock}");

        //        foreach (var node in nodes.Select((n, i) => (config:n, index:i)))
        //        {
        //            yield return new KeyValuePair<string, string>(
        //                $"ProtocolConfiguration:StandbyValidators:{node.index}", node.config.publicKey.EncodePoint(true).ToHexString());
        //            yield return new KeyValuePair<string, string>(
        //                $"ProtocolConfiguration:SeedList:{node.index}", $"{IPAddress.Loopback}:{node.config.tcpPort}");
        //        }
        //    }

        //    var config = new ConfigurationBuilder()
        //        .AddInMemoryCollection(settings())
        //        .Build();

        //    return ProtocolSettings.Initialize(config);
        //}

        //public static bool InitializeProtocolSettings(JsonDocument doc, uint secondsPerBlock = 15)
        //{
        //    return InitializeProtocolSettings(doc.RootElement, secondsPerBlock);
        //}

        //public void Write(Utf8JsonWriter writer)
        //{
        //    writer.WriteStartObject();
        //    writer.WriteNumber("magic", Magic);
        //    writer.WriteStartArray("consensus-nodes");
        //    foreach (var conensusNode in ConsensusNodes)
        //    {
        //        conensusNode.Write(writer);
        //    }
        //    writer.WriteEndArray();
        //    writer.WriteStartArray("wallets");
        //    foreach (var wallets in Wallets)
        //    {
        //        wallets.Write(writer);
        //    }
        //    writer.WriteEndArray();
        //    writer.WriteEndObject();
        //}
    }
}
