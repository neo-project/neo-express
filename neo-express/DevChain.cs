using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Neo.Express
{
    class DevChain
    {
        const uint MAINNET_MAGIC = 0x4F454Eu;
        const uint TESTNET_MAGIC = 0x544F454Eu;

        static uint GenerateMagicValue()
        {
            var random = new Random();

            do
            {
                uint magic = (uint)random.Next(int.MaxValue);

                // ensure the generated magic value isn't MainNet or TestNet's 
                // magic value
                if (!(magic == MAINNET_MAGIC || magic == TESTNET_MAGIC))
                {
                    return magic;
                }
            }
            while (true);
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

        public static DevChain FromJson(JsonElement json)
        {
            return new DevChain(
                json.GetProperty("magic").GetUInt32(),
                json.GetProperty("wallets").EnumerateArray().Select(DevWallet.FromJson));
        }

        public static DevChain FromJson(JsonDocument doc)
        {
            return FromJson(doc.RootElement);
        }

        // InitializeProtocolSettings works against the raw dev chain JSON file to avoid
        // default initialization of ProtocolSettings.
        public static bool InitializeProtocolSettings(JsonElement json, uint secondsPerBlock = 15)
        {
            var keyPairs = json.GetProperty("wallets")
                .EnumerateArray()
                .Select(DevWallet.KeyPairFromJson);

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
                wallet.WriteJson(writer);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
    }
}
