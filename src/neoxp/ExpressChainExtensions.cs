using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Wallets;
using NeoExpress.Models;

namespace NeoExpress
{
    static class ExpressChainExtensions
    {
        public static KeyPair GetKey(this ExpressWalletAccount account) => new KeyPair(account.PrivateKey.HexToBytes());

        public static UInt160 AsUInt160(this ExpressWalletAccount account) =>             throw new NotImplementedException();
// account.ScriptHash.ToScriptHash();

        public static Uri GetUri(this ExpressConsensusNode node)
            => new Uri($"http://localhost:{node.RpcPort}");

        public static bool IsMultiSigContract(this ExpressWalletAccount account)
            => Neo.SmartContract.Helper.IsMultiSigContract(account.Contract.Script.ToByteArray());

        public static bool IsMultiSigContract(this Neo.Wallets.WalletAccount account)
            => Neo.SmartContract.Helper.IsMultiSigContract(account.Contract.Script);

        public static IEnumerable<DevWallet> GetMultiSigWallets(this ExpressChain chain, ExpressWalletAccount account)
            => chain.ConsensusNodes
                .Select(n => n.Wallet)
                .Concat(chain.Wallets)
                .Where(w => w.Accounts.Find(a => a.ScriptHash == account.ScriptHash) != null)
                .Select(Models.DevWallet.FromExpressWallet);

        public static IEnumerable<DevWalletAccount> GetMultiSigAccounts(this ExpressChain chain, ExpressWalletAccount account)
            => chain.ConsensusNodes
                .Select(n => n.Wallet)
                .Concat(chain.Wallets)
                .Select(w => w.Accounts.Find(a => a.ScriptHash == account.ScriptHash))
                .Where(a => a != null)
                .Select(DevWalletAccount.FromExpressWalletAccount!);

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

        public static void InitalizeProtocolSettings(this ExpressChain chain, uint secondsPerBlock = 0)
        {
            if (!chain.TryInitializeProtocolSettings(secondsPerBlock))
            {
                throw new Exception("could not initialize protocol settings");
            }
        }

        public static bool TryInitializeProtocolSettings(this ExpressChain chain, uint secondsPerBlock = 0)
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
                        $"ProtocolConfiguration:SeedList:{index}", $"{System.Net.IPAddress.Loopback}:{node.TcpPort}");
                }
            }

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings())
                .Build();

            throw new NotImplementedException();

            // return ProtocolSettings.Initialize(config);
        }
    }
}
