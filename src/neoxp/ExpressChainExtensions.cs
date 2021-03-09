using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.Cryptography.ECC;
using Neo.Wallets;
using NeoExpress.Models;

namespace NeoExpress
{
    static class ExpressChainExtensions
    {
        public static KeyPair GetKey(this ExpressWalletAccount account) => new KeyPair(account.PrivateKey.HexToBytes());

        public static UInt160 AsUInt160(this ExpressWalletAccount account, ProtocolSettings settings) => AsUInt160(account, settings.AddressVersion);
        public static UInt160 AsUInt160(this ExpressWalletAccount account, byte version) => account.ScriptHash.ToScriptHash(version);

        public static Uri GetUri(this ExpressConsensusNode node) => new Uri($"http://localhost:{node.RpcPort}");

        public static bool IsMultiSigContract(this ExpressWalletAccount account)
            => Neo.SmartContract.Helper.IsMultiSigContract(account.Contract.Script.ToByteArray());

        public static bool IsMultiSigContract(this Neo.Wallets.WalletAccount account)
            => Neo.SmartContract.Helper.IsMultiSigContract(account.Contract.Script);

        public static IEnumerable<DevWallet> GetMultiSigWallets(this ExpressChain chain, ExpressWalletAccount account, ProtocolSettings? settings = null)
            => chain.ConsensusNodes
                .Select(n => n.Wallet)
                .Concat(chain.Wallets)
                .Where(w => w.Accounts.Find(a => a.ScriptHash == account.ScriptHash) != null)
                .Select(w => DevWallet.FromExpressWallet(settings ?? chain.GetProtocolSettings(), w));

        public static IEnumerable<DevWalletAccount> GetMultiSigAccounts(this ExpressChain chain, ExpressWalletAccount account, ProtocolSettings? settings = null)
            => chain.ConsensusNodes
                .Select(n => n.Wallet)
                .Concat(chain.Wallets)
                .Select(w => w.Accounts.Find(a => a.ScriptHash == account.ScriptHash))
                .Where(a => a != null)
                .Select(a => DevWalletAccount.FromExpressWalletAccount(settings ?? chain.GetProtocolSettings(), a!));

        public static Uri GetUri(this ExpressChain chain, int node = 0)
            => GetUri(chain.ConsensusNodes[node]);

        const string GENESIS = "genesis";

        public static bool IsReservedName(this ExpressChain chain, string name)
        {
            if (string.Equals(GENESIS, name, StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (var node in chain.ConsensusNodes)
            {
                if (string.Equals(node.Wallet.Name, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static ExpressWalletAccount? GetAccount(this ExpressChain chain, string name)
        {
            var wallet = (chain.Wallets ?? Enumerable.Empty<ExpressWallet>())
                .SingleOrDefault(w => name.Equals(w.Name, StringComparison.OrdinalIgnoreCase));
            if (wallet != null)
            {
                return wallet.DefaultAccount;
            }

            var node = chain.ConsensusNodes
                .SingleOrDefault(n => name.Equals(n.Wallet.Name, StringComparison.OrdinalIgnoreCase));
            if (node != null)
            {
                return node.Wallet.DefaultAccount;
            }

            if (GENESIS.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return chain.ConsensusNodes
                    .Select(n => n.Wallet.Accounts.Single(a => a.IsMultiSigContract()))
                    .Distinct(ExpressWalletAccountEqualityComparer.Instance)
                    .Single();
            }

            return null;
        }

        class ExpressWalletAccountEqualityComparer : EqualityComparer<ExpressWalletAccount>
        {
            public readonly static ExpressWalletAccountEqualityComparer Instance = new ExpressWalletAccountEqualityComparer();

            private ExpressWalletAccountEqualityComparer() {}

            public override bool Equals(ExpressWalletAccount? x, ExpressWalletAccount? y)
            {
                return x?.ScriptHash == y?.ScriptHash;
            }

            public override int GetHashCode([DisallowNull] ExpressWalletAccount obj)
            {
                return obj.ScriptHash.GetHashCode();
            }
        }

        public static ExpressWallet? GetWallet(this ExpressChain chain, string name)
            => (chain.Wallets ?? Enumerable.Empty<ExpressWallet>())
                .SingleOrDefault(w => string.Equals(name, w.Name, StringComparison.OrdinalIgnoreCase));
    }
}
