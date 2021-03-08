using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract;
using Neo.Wallets;
using NeoExpress.Models;

namespace NeoExpress
{
    internal class ExpressChainManagerFactory : IExpressChainManagerFactory
    {
        readonly IFileSystem fileSystem;

        public ExpressChainManagerFactory(IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        internal const string EXPRESS_EXTENSION = ".neo-express";
        internal const string DEFAULT_EXPRESS_FILENAME = "default" + EXPRESS_EXTENSION;

        string ResolveChainFileName(string path) => fileSystem.ResolveFileName(path, EXPRESS_EXTENSION, () => "default");

        internal static ExpressChain CreateChain(int nodeCount, byte? addressVersion)
        {
            if (nodeCount != 1 && nodeCount != 4 && nodeCount != 7)
            {
                throw new ArgumentException("invalid blockchain node count", nameof(nodeCount));
            }

            var settings = ProtocolSettings.Default with 
            { 
                Magic = ExpressChain.GenerateMagicValue(),
                AddressVersion = addressVersion ?? ProtocolSettings.Default.AddressVersion 
            };

            var wallets = new List<(DevWallet wallet, WalletAccount account)>(nodeCount);
            for (var i = 1; i <= nodeCount; i++)
            {
                var wallet = new DevWallet(settings, $"node{i}");
                var account = wallet.CreateAccount();
                account.IsDefault = true;
                wallets.Add((wallet, account));
            }

            var keys = wallets.Select(t => t.account.GetKey().PublicKey).ToArray();
            var contract = Contract.CreateMultiSigContract((keys.Length * 2 / 3) + 1, keys);

            foreach (var (wallet, account) in wallets)
            {
                var multiSigContractAccount = wallet.CreateAccount(contract, account.GetKey());
                multiSigContractAccount.Label = "Consensus MultiSigContract";
            }

            var nodes = wallets.Select((w, i) => new ExpressConsensusNode
                {
                    TcpPort = GetPortNumber(i, 3),
                    WebSocketPort = GetPortNumber(i, 4),
                    RpcPort = GetPortNumber(i, 2),
                    Wallet = w.wallet.ToExpressWallet()
                });

            return new ExpressChain()
            {
                Magic = settings.Magic,
                AddressVersion = settings.AddressVersion,
                ConsensusNodes = nodes.ToList(),
            };

            // 49152 is the first port in the "Dynamic and/or Private" range as specified by IANA
            // http://www.iana.org/assignments/port-numbers
            static ushort GetPortNumber(int index, ushort portNumber) => (ushort)(50000 + ((index + 1) * 10) + portNumber);
        }

        public (IExpressChainManager manager, string path) CreateChain(int nodeCount, byte? addressVersion, string output, bool force)
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

            var chain = CreateChain(nodeCount, addressVersion);
            return (new ExpressChainManager(fileSystem, chain), output);
        }

        public (IExpressChainManager manager, string path) LoadChain(string path)
        {
            path = ResolveChainFileName(path);
            if (!fileSystem.File.Exists(path))
            {
                throw new Exception($"{path} file doesn't exist");
            }

            var chain = fileSystem.LoadChain(path);
            return (new ExpressChainManager(fileSystem, chain), path);
        }
    }
}
