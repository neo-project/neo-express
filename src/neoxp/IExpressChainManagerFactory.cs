using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using NeoExpress.Models;
using Newtonsoft.Json;

namespace NeoExpress
{
    internal interface IExpressChainManagerFactory
    {
        (IExpressChainManager manager, string path) CreateChain(int nodeCount, string outputPath, bool force);
        (IExpressChainManager manager, string path) LoadChain(string path);
    }

    internal class ExpressChainManagerFactory : IExpressChainManagerFactory
    {
        readonly IFileSystem fileSystem;

        public ExpressChainManagerFactory(IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        internal const string EXPRESS_EXTENSION = ".neo-express";
        internal const string DEFAULT_EXPRESS_FILENAME = "default" + EXPRESS_EXTENSION;

        string ResolveChainFileName(string filename) => fileSystem.ResolveFileName(filename, EXPRESS_EXTENSION, () => "default");

        public (IExpressChainManager manager, string path) CreateChain(int nodeCount, string output, bool force)
        {
            output = ResolveChainFileName(output);
            if (fileSystem.File.Exists(output))
            {
                if (force)
                {
                    fileSystem.File.Delete(output);
                }
                else
                {
                    throw new Exception("You must specify --force to overwrite an existing file");
                }
            }

            if (fileSystem.File.Exists(output))
            {
                throw new ArgumentException($"{output} already exists", nameof(output));
            }

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

            var chain = new ExpressChain()
            {
                Magic = ExpressChain.GenerateMagicValue(),
                ConsensusNodes = nodes,
            };

            return (new ExpressChainManager(fileSystem, chain), output);

            static ushort GetPortNumber(int index, ushort portNumber) => (ushort)(50000 + ((index + 1) * 10) + portNumber);
        }

        public (IExpressChainManager manager, string path) LoadChain(string path)
        {
            path = ResolveChainFileName(path);
            if (!fileSystem.File.Exists(path))
            {
                throw new Exception($"{path} file doesn't exist");
            }
            var serializer = new JsonSerializer();
            using var stream = fileSystem.File.OpenRead(path);
            using var reader = new JsonTextReader(new System.IO.StreamReader(stream));
            var chain = serializer.Deserialize<ExpressChain>(reader)
                ?? throw new Exception($"Cannot load Neo-Express instance information from {path}");

            return (new ExpressChainManager(fileSystem, chain), path);
        }
    }
}
