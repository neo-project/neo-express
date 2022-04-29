using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Linq;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using NeoExpress.Models;

namespace NeoExpress
{
    class ExpressFile : IExpressFile
    {
        readonly IFileSystem fileSystem;
        readonly string ChainPath;

        public ExpressChain Chain { get; }

        public ExpressFile(string input, IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
            (Chain, ChainPath) = fileSystem.LoadExpressChain(input);
        }

        public void Save()
        {
            fileSystem.SaveChain(Chain, ChainPath);
        }

        public IExpressNode GetExpressNode(bool offlineTrace = false)
        {
            // Check to see if there's a neo-express blockChain currently running by
            // attempting to open a mutex with the multisig account address for a name

            for (int i = 0; i < Chain.ConsensusNodes.Count; i++)
            {
                var consensusNode = Chain.ConsensusNodes[i];
                if (consensusNode.IsRunning())
                {
                    return new Node.OnlineNode(this, consensusNode);
                }
            }

            var node = Chain.ConsensusNodes[0];
            var nodePath = fileSystem.GetNodePath(node);
            if (!fileSystem.Directory.Exists(nodePath)) fileSystem.Directory.CreateDirectory(nodePath);
            var provider = RocksDbStorageProvider.Open(nodePath);
            return new Node.OfflineNode(this, node, provider, offlineTrace);
        }

        public bool TryResolveSigner(string name, string password, [MaybeNullWhen(false)] out Neo.Wallets.Wallet wallet, out UInt160 accountHash)
        {
            if (!string.IsNullOrEmpty(name))
            {
                var settings = Chain.GetProtocolSettings();

                if (name.Equals(ExpressChainExtensions.GENESIS, StringComparison.OrdinalIgnoreCase))
                {
                    var contract = Chain.CreateGenesisContract();
                    wallet = new DevWallet(settings, string.Empty);
                    accountHash = wallet.CreateAccount(contract).ScriptHash;
                    return true;
                }

                var wallets = (Chain.Wallets as IReadOnlyList<ExpressWallet>) ?? Array.Empty<ExpressWallet>();
                for (int i = 0; i < wallets.Count; i++)
                {
                    if (name.Equals(wallets[i].Name, StringComparison.OrdinalIgnoreCase))
                    {
                        wallet = DevWallet.FromExpressWallet(settings, wallets[i]);
                        accountHash = wallet.GetAccounts().Single(a => a.IsDefault).ScriptHash;
                        return true;
                    }
                }

                for (int i = 0; i < Chain.ConsensusNodes.Count; i++)
                {
                    var nodeWallet = Chain.ConsensusNodes[i].Wallet;
                    if (name.Equals(nodeWallet.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        wallet = DevWallet.FromExpressWallet(settings, nodeWallet);
                        accountHash = wallet.GetAccounts().Single(a => a.IsDefault).ScriptHash;
                        return true;
                    }
                }

                if (TryGetWif(name, out var privateKey))
                {
                    wallet = new DevWallet(settings, string.Empty);
                    accountHash = wallet.CreateAccount(privateKey).ScriptHash;
                    return true;
                }

                if (!string.IsNullOrEmpty(password))
                {
                    if (TryGetNEP2(name, password, settings, out privateKey))
                    {
                        wallet = new DevWallet(settings, string.Empty);
                        accountHash = wallet.CreateAccount(privateKey).ScriptHash;
                        return true;
                    }

                    if (fileSystem.TryImportNEP6(name, password, settings, out var devWallet))
                    {
                        var account = devWallet.GetAccounts().SingleOrDefault(a => a.IsDefault)
                            ?? devWallet.GetAccounts().SingleOrDefault()
                            ?? throw new InvalidOperationException("Neo-express only supports NEP-6 wallets with a single default account or a single account");
                        if (account.IsMultiSigContract())
                        {
                            throw new Exception("Neo-express doesn't supports multi-sig NEP-6 accounts");
                        }

                        wallet = devWallet;
                        accountHash = account.ScriptHash;

                        return true;
                    }
                }
            }

            wallet = null;
            accountHash = UInt160.Zero;
            return false;

            static bool TryGetWif(string wif, out byte[] privateKey)
            {
                try
                {
                    privateKey = Neo.Wallets.Wallet.GetPrivateKeyFromWIF(wif);
                    return true;
                }
                catch (System.Exception)
                {
                    privateKey = Array.Empty<byte>();
                    return false;
                }
            }

            static bool TryGetNEP2(string nep2, string password, ProtocolSettings settings, out byte[] privateKey)
            {
                try
                {
                    privateKey = Neo.Wallets.Wallet.GetPrivateKeyFromNEP2(nep2, password, settings.AddressVersion);
                    return true;
                }
                catch
                {
                    privateKey = Array.Empty<byte>();
                    return false;
                }
            }
        }

    }
}
