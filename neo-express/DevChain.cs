using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;

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

        public uint Magic { get; set; }
        public List<DevWallet> Wallets { get; set; }

        public DevChain(uint magic, IEnumerable<DevWallet> wallets)
        {
            Magic = magic;
            Wallets = wallets.ToList();
        }

        public DevChain(IEnumerable<DevWallet> wallets) 
            : this(GenerateMagicValue(), wallets)
        {
        }

        public static DevChain Parse(JsonElement json)
        {
            return new DevChain(
                json.GetProperty("magic").GetUInt32(),
                json.GetProperty("wallets").EnumerateArray().Select(DevWallet.Parse));
        }

        public static DevChain Parse(JsonDocument doc)
        {
            return Parse(doc.RootElement);
        }

        // InitializeProtocolSettings uses the dev chain's raw JSON information 
        // to avoid default initialization of ProtocolSettings.
        public static bool InitializeProtocolSettings(JsonElement json, uint secondsPerBlock = 0)
        {
            var keyPairs = json.GetProperty("wallets")
                .EnumerateArray()
                .Select(DevWallet.ParseKeyPair);

            secondsPerBlock = secondsPerBlock == 0 ? 15 : secondsPerBlock;

            IEnumerable<KeyValuePair<string, string>> settings()
            {
                yield return new KeyValuePair<string, string>(
                    "ProtocolConfiguration:Magic", $"{json.GetProperty("magic").GetUInt32()}");
                yield return new KeyValuePair<string, string>(
                    "ProtocolConfiguration:AddressVersion", $"{(byte)0x17}");
                yield return new KeyValuePair<string, string>(
                    "ProtocolConfiguration:SecondsPerBlock", $"{secondsPerBlock}");

                foreach (var (keypair, index) in keyPairs.Select((pk, i) => (pk, i)))
                {
                    yield return new KeyValuePair<string, string>(
                        $"ProtocolConfiguration:StandbyValidators:{index}", keypair.PublicKey.EncodePoint(true).ToHexString());
                    yield return new KeyValuePair<string, string>(
                        $"ProtocolConfiguration:SeedList:{index}", $"{IPAddress.Loopback}:{((index + 1) * 10000) + 1}");
                }
            }

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings())
                .Build();

            return ProtocolSettings.Initialize(config);
        }

        public static bool InitializeProtocolSettings(JsonDocument doc, uint secondsPerBlock = 15)
        {
            return InitializeProtocolSettings(doc.RootElement);
        }

        public void WriteJson(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WriteNumber("magic", Magic);
            writer.WriteStartArray("wallets");
            foreach (var wallet in Wallets)
            {
                wallet.Write(writer);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
    }
}
