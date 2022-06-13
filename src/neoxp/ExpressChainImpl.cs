using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Linq;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.Wallets;

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
            (chainInfo, chainPath) = fileSystem.LoadExpressChainInfo(input);
            chainInfo.Wallets ??= new List<ExpressWallet>();
            this.fileSystem = fileSystem;
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
            throw new System.NotImplementedException();
        }

        public string GetNodePath(ExpressConsensusNode node)
        {
            ArgumentNullException.ThrowIfNull(node);
            ArgumentNullException.ThrowIfNull(node.Wallet);

            var account = node.Wallet.Accounts.Single(a => a.IsDefault);

            var rootPath = fileSystem.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify),
                "Neo-Express",
                "blockchain-nodes");
            return fileSystem.Path.Combine(rootPath, account.ScriptHash);
        }

        public bool TryResolveSigner(string name, string password, [MaybeNullWhen(false)] out Wallet wallet, out UInt160 accountHash)
        {
            throw new System.NotImplementedException();
        }
    }
}
