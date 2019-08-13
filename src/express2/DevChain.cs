using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using Neo;

namespace NeoExpress.Neo2Backend
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
        public List<DevContract> Contracts { get; set; }

        public DevChain(uint magic, IEnumerable<DevConsensusNode> consensusNodes, IEnumerable<DevWallet> wallets = null, IEnumerable<DevContract> contracts = null)
        {
            Magic = magic;
            ConsensusNodes = consensusNodes.ToList();
            Wallets = wallets == null ? new List<DevWallet>() : wallets.ToList();
            Contracts = contracts == null ? new List<DevContract>() : contracts.ToList();
        }

        public DevChain(IEnumerable<DevConsensusNode> consensusNodes, IEnumerable<DevWallet> wallets = null, IEnumerable<DevContract> contracts = null)
            : this(GenerateMagicValue(), consensusNodes, wallets, contracts)
        {
        }

        public static bool IsGenesis(string name) => string.Compare(name, "genesis", true) == 0;

        public bool IsReservedName(string name)
        {
            if (IsGenesis(name))
                return true;

            foreach (var node in ConsensusNodes)
            {
                if (node.Wallet.NameMatches(name))
                    return true;
            }

            return false;
        }

        public DevWallet GetWallet(string name) => Wallets.SingleOrDefault(w => w.NameMatches(name));

        public Uri GetUri(int node = 0) =>new Uri($"http://localhost:{ConsensusNodes[node].RpcPort}");

        private class DevWalletAccountComparer : IEqualityComparer<DevWalletAccount>
        {
            public bool Equals(DevWalletAccount x, DevWalletAccount y)
            {
                return x.ScriptHash.Equals(y.ScriptHash);
            }

            public int GetHashCode(DevWalletAccount obj)
            {
                int hash = default(byte).GetHashCode();
                var scriptHashArray = obj.ScriptHash.ToArray();
                for (int i = 0; i < scriptHashArray.Length; i++)
                {
                    hash = HashCode.Combine(hash, i, scriptHashArray[i]);
                }
                return hash;
            }
        }

        public DevWalletAccount GetAccount(string name)
        {
            var wallet = Wallets.SingleOrDefault(w => w.NameMatches(name));
            if (wallet != default)
            {
                return wallet.DefaultAccount;
            }

            var node = ConsensusNodes.SingleOrDefault(n => n.Wallet.NameMatches(name));
            if (node != default)
            {
                return node.Wallet.DefaultAccount;
            }

            if (string.Compare(name, "genesis", true) == 0)
            {
                return ConsensusNodes
                    .Select(n => n.Wallet.GetAccounts().Cast<DevWalletAccount>().Single(a => a.Label == "MultiSigContract"))
                    .Distinct(new DevWalletAccountComparer())
                    .Single();
            }

            return default;
        }

        public static DevChain FromJson(JObject json)
        {
            var magic = json.Value<uint>("magic");
            var consensusNodes = json["consensus-nodes"].Select(DevConsensusNode.FromJson);
            var wallets = json["wallets"].Select(DevWallet.FromJson);
            var contracts = json["contracts"].Select(DevContract.FromJson);
            return new DevChain(magic, consensusNodes, wallets, contracts);
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

            writer.WritePropertyName("contracts");
            writer.WriteStartArray();
            foreach (var contract in Contracts)
            {
                contract.ToJson(writer);
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        public static string GetDefaultFilename(string filename) => string.IsNullOrEmpty(filename)
               ? Path.Combine(Directory.GetCurrentDirectory(), "default.neo-express.json")
               : filename;

        public static (JObject json, string filename) LoadJson(string filename)
        {
            filename = GetDefaultFilename(filename);
            if (!File.Exists(filename))
            {
                throw new Exception($"{filename} file doesn't exist");
            }

            using (var stream = File.OpenRead(filename))
            using (var reader = new JsonTextReader(new StreamReader(stream)))
            {
                return (JObject.Load(reader), filename);
            }
        }

        public static (DevChain devChain, string filename) Load(string filename)
        {
            var (json, _filename) = LoadJson(filename);
            return (FromJson(json), _filename);
        }

        public static DevChain Initialize(string filename, uint secondsPerBlock)
        {
            var (devChainJson, _) = LoadJson(filename);
            if (!InitializeProtocolSettings(devChainJson, secondsPerBlock))
            {
                throw new Exception("Couldn't initialize protocol settings");
            }

            return FromJson(devChainJson);
        }

        public static DevChain Initialize(JObject json, uint secondsPerBlock)
        {
            if (!InitializeProtocolSettings(json, secondsPerBlock))
            {
                throw new Exception("Couldn't initialize protocol settings");
            }

            return FromJson(json);
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
