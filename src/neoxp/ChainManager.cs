using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Configuration;
using Neo;
using NeoExpress.Models;
using Newtonsoft.Json;

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
    }
}
