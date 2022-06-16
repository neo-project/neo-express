using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Linq;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract;
using NeoExpress.Models;
using NeoExpress.Node;

namespace NeoExpress
{
    using ExpressChainInfo = Neo.BlockchainToolkit.Models.ExpressChain;

    // TODO: rename as just ExpressChain once everything is moved over to the new interface
    class ExpressChainImpl : IExpressChain
    {
        readonly ExpressChainInfo chainInfo;
        readonly string chainPath;
        readonly IFileSystem fileSystem;

        public uint Network => chainInfo.Network;
        public byte AddressVersion => chainInfo.AddressVersion;
        public IReadOnlyList<ExpressConsensusNode> ConsensusNodes => chainInfo.ConsensusNodes;
        public IReadOnlyList<ExpressWallet> Wallets => chainInfo.Wallets;
        public IReadOnlyDictionary<string, string> Settings
            => (chainInfo.Settings as IReadOnlyDictionary<string, string>)
                ?? ImmutableDictionary<string, string>.Empty;

        public ExpressChainImpl(string input, IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
            chainPath = fileSystem.ResolveExpressFileName(input);
            if (!fileSystem.File.Exists(chainPath))
            {
                throw new Exception($"{input} file doesn't exist");
            }

            chainInfo = fileSystem.LoadChain(chainPath);
            chainInfo.Wallets ??= new List<ExpressWallet>();

            // validate neo-express file by ensuring stored node zero default account SignatureRedeemScript matches a generated script
            var account = chainInfo.ConsensusNodes[0].Wallet.DefaultAccount ?? throw new InvalidOperationException("consensus node 0 missing default account");
            var keyPair = new Neo.Wallets.KeyPair(Convert.FromHexString(account.PrivateKey));
            var contractScript = Convert.FromHexString(account.Contract.Script);

            if (!Contract.CreateSignatureRedeemScript(keyPair.PublicKey).AsSpan().SequenceEqual(contractScript))
            {
                throw new Exception("Invalid Signature Redeem Script. Was this neo-express file created before RC1?");
            }
        }

        public void AddWallet(ExpressWallet wallet)
        {
            if (this.IsReservedName(wallet.Name)) throw new ArgumentException(nameof(wallet));
            chainInfo.Wallets.Add(wallet);
        }

        public void RemoveWallet(ExpressWallet wallet)
        {
            chainInfo.Wallets.Remove(wallet);
        }

        public void SaveChain()
        {
            fileSystem.SaveChain(chainInfo, chainPath);
        }

        public IExpressNode GetExpressNode(bool offlineTrace = false)
        {
            // Check to see if there's a neo-express blockChain currently running by
            // attempting to open a mutex with the multisig account address for a name

            for (int i = 0; i < chainInfo.ConsensusNodes.Count; i++)
            {
                var consensusNode = chainInfo.ConsensusNodes[i];
                if (consensusNode.IsRunning())
                {
                    return new OnlineNode(this, consensusNode);
                }
            }

            var node = chainInfo.ConsensusNodes[0];
            var nodePath = fileSystem.GetNodePath(node);
            if (!fileSystem.Directory.Exists(nodePath)) fileSystem.Directory.CreateDirectory(nodePath);
            var expressStorage = new RocksDbExpressStorage(nodePath);
            return new OfflineNode(this, expressStorage, offlineTrace);
        }

        public bool TryResolveSigner(string name, string password, [MaybeNullWhen(false)] out Neo.Wallets.Wallet wallet, out UInt160 accountHash)
        {
            if (!string.IsNullOrEmpty(name))
            {
                var settings = this.GetProtocolSettings();
                if (name.Equals(IExpressChain.GENESIS, StringComparison.OrdinalIgnoreCase))
                {
                    var contract = chainInfo.CreateGenesisContract();
                    wallet = new DevWallet(settings, string.Empty);
                    accountHash = wallet.CreateAccount(contract).ScriptHash;
                    return true;
                }

                var wallets = (chainInfo.Wallets as IReadOnlyList<ExpressWallet>) ?? Array.Empty<ExpressWallet>();
                for (int i = 0; i < wallets.Count; i++)
                {
                    if (name.Equals(wallets[i].Name, StringComparison.OrdinalIgnoreCase))
                    {
                        wallet = DevWallet.FromExpressWallet(wallets[i], settings);
                        accountHash = wallet.GetAccounts().Single(a => a.IsDefault).ScriptHash;
                        return true;
                    }
                }

                for (int i = 0; i < chainInfo.ConsensusNodes.Count; i++)
                {
                    var nodeWallet = chainInfo.ConsensusNodes[i].Wallet;
                    if (name.Equals(nodeWallet.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        wallet = DevWallet.FromExpressWallet(nodeWallet, settings);
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
                    if (TryGetNEP2(name, password, chainInfo.AddressVersion, out privateKey))
                    {
                        wallet = new DevWallet(settings, string.Empty);
                        accountHash = wallet.CreateAccount(privateKey).ScriptHash;
                        return true;
                    }

                    if (TryImportNEP6(fileSystem, name, password, settings, out var devWallet))
                    {
                        var account = devWallet.GetAccounts().SingleOrDefault(a => a.IsDefault)
                            ?? devWallet.GetAccounts().SingleOrDefault()
                            ?? throw new InvalidOperationException("Neo-express only supports NEP-6 wallets with a single default account or a single account");
                        if (Neo.SmartContract.Helper.IsMultiSigContract(account.Contract.Script))
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

            static bool TryGetNEP2(string nep2, string password, byte addressVersion, out byte[] privateKey)
            {
                try
                {
                    privateKey = Neo.Wallets.Wallet.GetPrivateKeyFromNEP2(nep2, password, addressVersion);
                    return true;
                }
                catch
                {
                    privateKey = Array.Empty<byte>();
                    return false;
                }
            }

            static bool TryImportNEP6(IFileSystem fileSystem, string path, string password, ProtocolSettings settings, [MaybeNullWhen(false)] out DevWallet wallet)
            {
                if (fileSystem.File.Exists(path))
                {
                    var json = Neo.IO.Json.JObject.Parse(fileSystem.File.ReadAllBytes(path));
                    var nep6wallet = new Neo.Wallets.NEP6.NEP6Wallet(string.Empty, password, settings, json);

                    wallet = new DevWallet(settings, nep6wallet.Name);
                    foreach (var account in nep6wallet.GetAccounts())
                    {
                        var devAccount = wallet.CreateAccount(account.Contract, account.GetKey());
                        devAccount.Label = account.Label;
                        devAccount.IsDefault = account.IsDefault;
                    }

                    return true;
                }

                wallet = null;
                return false;
            }

        }
    }
}
