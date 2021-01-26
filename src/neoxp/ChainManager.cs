using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Configuration;
using Neo;
using NeoExpress.Models;
using Newtonsoft.Json;

using FileAccess = System.IO.FileAccess;
using FileMode = System.IO.FileMode;
using StreamWriter = System.IO.StreamWriter;
using TextWriter = System.IO.TextWriter;

namespace NeoExpress
{
    class ChainManager : IChainManager
    {
        readonly IFileSystem fileSystem;

        public ChainManager(IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public ExpressChain Create(int nodeCount)
        {
            if (nodeCount != 1 && nodeCount != 4 && nodeCount != 7)
            {
                throw new ArgumentException("invalid blockchain node count", nameof(nodeCount));
            }

            var wallets = new List<(DevWallet wallet, Neo.Wallets.WalletAccount account)>(nodeCount);

            for (var i = 1; i <= nodeCount; i++)
            {
                var wallet = new DevWallet($"node{i}");
                var account = wallet.CreateAccount();
                account.IsDefault = true;
                wallets.Add((wallet, account));
            }

            var keys = wallets.Select(t => t.account.GetKey().PublicKey).ToArray();

            var contract = Neo.SmartContract.Contract.CreateMultiSigContract((keys.Length * 2 / 3) + 1, keys);

            foreach (var (wallet, account) in wallets)
            {
                var multiSigContractAccount = wallet.CreateAccount(contract, account.GetKey());
                multiSigContractAccount.Label = "MultiSigContract";
            }

            // 49152 is the first port in the "Dynamic and/or Private" range as specified by IANA
            // http://www.iana.org/assignments/port-numbers
            var nodes = new List<ExpressConsensusNode>(nodeCount);
            for (var i = 0; i < nodeCount; i++)
            {
                nodes.Add(new ExpressConsensusNode()
                {
                    TcpPort = GetPortNumber(i, 3),
                    WebSocketPort = GetPortNumber(i, 4),
                    RpcPort = GetPortNumber(i, 2),
                    Wallet = wallets[i].wallet.ToExpressWallet()
                });
            }

            return new ExpressChain()
            {
                Magic = ExpressChain.GenerateMagicValue(),
                ConsensusNodes = nodes,
            };

            static ushort GetPortNumber(int index, ushort portNumber) => (ushort)(50000 + ((index + 1) * 10) + portNumber);
        }

        internal const string EXPRESS_EXTENSION = ".neo-express";
        internal const string DEFAULT_EXPRESS_FILENAME = "default" + EXPRESS_EXTENSION;

        public string ResolveFileName(string filename) 
        {
            if (string.IsNullOrEmpty(filename))
            {
                return fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), DEFAULT_EXPRESS_FILENAME);
            }

            filename = fileSystem.Path.IsPathFullyQualified(filename)
                ? filename : fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), filename);

            return fileSystem.Path.GetExtension(filename) != EXPRESS_EXTENSION
                ? filename + EXPRESS_EXTENSION : filename;
        }

        public (Models.ExpressChain chain, string filename) Load(string filename)
        {
            filename = ResolveFileName(filename);
            if (!fileSystem.File.Exists(filename))
            {
                throw new Exception($"{filename} file doesn't exist");
            }
            var serializer = new JsonSerializer();
            using var stream = fileSystem.File.OpenRead(filename);
            using var reader = new JsonTextReader(new System.IO.StreamReader(stream));
            var chain = serializer.Deserialize<ExpressChain>(reader)
                ?? throw new Exception($"Cannot load Neo-Express instance information from {filename}");

            return (chain, filename);
        }


        public void Save(ExpressChain chain, string fileName)
        {
            var serializer = new JsonSerializer();
            using (var stream = fileSystem.File.Open(fileName, System.IO.FileMode.Create, System.IO.FileAccess.Write))
            using (var writer = new JsonTextWriter(new System.IO.StreamWriter(stream)) { Formatting = Formatting.Indented })
            {
                serializer.Serialize(writer, chain);
            }
        }

        public bool InitializeProtocolSettings(ExpressChain chain, uint secondsPerBlock = 0)
        {
            secondsPerBlock = secondsPerBlock == 0 ? 15 : secondsPerBlock;

            IEnumerable<KeyValuePair<string, string>> settings()
            {
                yield return new KeyValuePair<string, string>(
                    "ProtocolConfiguration:Magic", $"{chain.Magic}");
                yield return new KeyValuePair<string, string>(
                    "ProtocolConfiguration:MillisecondsPerBlock", $"{secondsPerBlock * 1000}");
                yield return new KeyValuePair<string, string>(
                    "ProtocolConfiguration:ValidatorsCount", $"{chain.ConsensusNodes.Count}");

                foreach (var (node, index) in chain.ConsensusNodes.Select((n, i) => (n, i)))
                {
                    var privateKey = node.Wallet.Accounts
                        .Select(a => a.PrivateKey)
                        .Distinct().Single().HexToBytes();
                    var encodedPublicKey = new Neo.Wallets.KeyPair(privateKey).PublicKey
                        .EncodePoint(true).ToHexString();
                    yield return new KeyValuePair<string, string>(
                        $"ProtocolConfiguration:StandbyCommittee:{index}", encodedPublicKey);
                    yield return new KeyValuePair<string, string>(
                        $"ProtocolConfiguration:SeedList:{index}", $"{IPAddress.Loopback}:{node.TcpPort}");
                }
            }

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings())
                .Build();

            return ProtocolSettings.Initialize(config);
        }

        public void Export(ExpressChain chain, string password, TextWriter writer)
        {
            var folder = fileSystem.Directory.GetCurrentDirectory();
            for (var i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var node = chain.ConsensusNodes[i];
                writer.WriteLine($"Exporting {node.Wallet.Name} Conensus Node wallet");

                var walletPath = fileSystem.Path.Combine(folder, $"{node.Wallet.Name}.wallet.json");
                if (fileSystem.File.Exists(walletPath))
                {
                    fileSystem.File.Delete(walletPath);
                }

                var devWallet = DevWallet.FromExpressWallet(node.Wallet);
                devWallet.Export(walletPath, password);

                var path = fileSystem.Path.Combine(folder, $"{node.Wallet.Name}.config.json");
                using var stream = fileSystem.File.Open(path, FileMode.Create, FileAccess.Write);
                using var configWriter = new JsonTextWriter(new StreamWriter(stream)) { Formatting = Formatting.Indented };

                // use neo-cli defaults for Logger & Storage

                configWriter.WriteStartObject();
                configWriter.WritePropertyName("ApplicationConfiguration");
                configWriter.WriteStartObject();

                configWriter.WritePropertyName("P2P");
                configWriter.WriteStartObject();
                configWriter.WritePropertyName("Port");
                configWriter.WriteValue(node.TcpPort);
                configWriter.WritePropertyName("WsPort");
                configWriter.WriteValue(node.WebSocketPort);
                configWriter.WriteEndObject();

                configWriter.WritePropertyName("UnlockWallet");
                configWriter.WriteStartObject();
                configWriter.WritePropertyName("Path");
                configWriter.WriteValue(walletPath);
                configWriter.WritePropertyName("Password");
                configWriter.WriteValue(password);
                configWriter.WritePropertyName("StartConsensus");
                configWriter.WriteValue(true);
                configWriter.WritePropertyName("IsActive");
                configWriter.WriteValue(true);
                configWriter.WriteEndObject();

                configWriter.WriteEndObject();
                configWriter.WriteEndObject();
            }

            {
                var path = fileSystem.Path.Combine(folder, "protocol.json");
                using var stream = fileSystem.File.Open(path, FileMode.Create, FileAccess.Write);
                using var protocolWriter = new JsonTextWriter(new StreamWriter(stream)) { Formatting = Formatting.Indented };

                // use neo defaults for MillisecondsPerBlock & AddressVersion

                protocolWriter.WriteStartObject();
                protocolWriter.WritePropertyName("ProtocolConfiguration");
                protocolWriter.WriteStartObject();

                protocolWriter.WritePropertyName("Magic");
                protocolWriter.WriteValue(chain.Magic);
                protocolWriter.WritePropertyName("ValidatorsCount");
                protocolWriter.WriteValue(chain.ConsensusNodes.Count);

                protocolWriter.WritePropertyName("StandbyCommittee");
                protocolWriter.WriteStartArray();
                for (int i = 0; i < chain.ConsensusNodes.Count; i++)
                {
                    var account = DevWalletAccount.FromExpressWalletAccount(chain.ConsensusNodes[i].Wallet.DefaultAccount);
                    var key = account.GetKey();
                    if (key != null)
                    {
                        protocolWriter.WriteValue(key.PublicKey.EncodePoint(true).ToHexString());
                    }
                }
                protocolWriter.WriteEndArray();

                protocolWriter.WritePropertyName("SeedList");
                protocolWriter.WriteStartArray();
                foreach (var node in chain.ConsensusNodes)
                {
                    protocolWriter.WriteValue($"{IPAddress.Loopback}:{node.TcpPort}");
                }
                protocolWriter.WriteEndArray();

                protocolWriter.WriteEndObject();
                protocolWriter.WriteEndObject();
            }
        }
    }
}
