using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract;
using NeoExpress.Models;
using NeoExpress.Node;

namespace NeoExpress
{
    static class ExpressFileExtensions
    {
        public static ProtocolSettings GetProtocolSettings(this IExpressChain? chain, uint secondsPerBlock = 0)
        {
            return chain == null
                ? ProtocolSettings.Default
                : ProtocolSettings.Default with
                {
                    Network = chain.Network,
                    AddressVersion = chain.AddressVersion,
                    MillisecondsPerBlock = secondsPerBlock == 0 ? 15000 : secondsPerBlock * 1000,
                    ValidatorsCount = chain.ConsensusNodes.Count,
                    StandbyCommittee = chain.ConsensusNodes.Select(GetPublicKey).ToArray(),
                    SeedList = chain.ConsensusNodes
                        .Select(n => $"{System.Net.IPAddress.Loopback}:{n.TcpPort}")
                        .ToArray(),
                };

            static Neo.Cryptography.ECC.ECPoint GetPublicKey(ExpressConsensusNode node)
                => new Neo.Wallets.KeyPair(node.Wallet.Accounts.Select(a => a.PrivateKey).Distinct().Single().HexToBytes()).PublicKey;
        }

        public static Neo.Wallets.KeyPair[] GetConsensusNodeKeys(this IExpressChain chain)
            => chain.ConsensusNodes
                .Select(n => n.Wallet.DefaultAccount ?? throw new Exception($"{n.Wallet.Name} missing default account"))
                .Select(a => new Neo.Wallets.KeyPair(a.PrivateKey.HexToBytes()))
                .ToArray();

        static Contract GetConsensusContract(this IExpressChain chain)
        {
            List<Neo.Cryptography.ECC.ECPoint> publicKeys = new(chain.ConsensusNodes.Count);
            foreach (var node in chain.ConsensusNodes)
            {
                var account = node.Wallet.DefaultAccount ?? throw new Exception("Missing Default Account");
                var privateKey = Convert.FromHexString(account.PrivateKey);
                var keyPair = new Neo.Wallets.KeyPair(privateKey);
                publicKeys.Add(keyPair.PublicKey);
            }

            var m = publicKeys.Count * 2 / 3 + 1;
            return Contract.CreateMultiSigContract(m, publicKeys);
        }

        public static UInt160 GetConsensusScriptHash(this IExpressChain chain)
        {
            var contract = chain.GetConsensusContract();
            return contract.ScriptHash;
        }

        public static bool IsReservedName(this IExpressChain chain, string name)
        {
            if (string.Equals(IExpressChain.GENESIS, name, StringComparison.OrdinalIgnoreCase))
                return true;

            for (int i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                if (string.Equals(chain.ConsensusNodes[i].Wallet.Name, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static ExpressWallet? GetWallet(this IExpressChain chain, string name)
            => (chain.Wallets ?? Enumerable.Empty<ExpressWallet>())
                .SingleOrDefault(w => string.Equals(name, w.Name, StringComparison.OrdinalIgnoreCase));


















        public static IReadOnlyList<Neo.Wallets.Wallet> GetMultiSigWallets(this IExpressChain chain, ProtocolSettings settings, UInt160 accountHash)
        {
            var wallets = new List<DevWallet>();
            for (int i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var wallet = DevWallet.FromExpressWallet(settings, chain.ConsensusNodes[i].Wallet);
                if (wallet.GetAccount(accountHash) != null) wallets.Add(wallet);
            }

            for (int i = 0; i < chain.Wallets.Count; i++)
            {
                var wallet = DevWallet.FromExpressWallet(settings, chain.Wallets[i]);
                if (wallet.GetAccount(accountHash) != null) wallets.Add(wallet);
            }

            return wallets;
        }

        public delegate bool TryParse<T>(string value, [MaybeNullWhen(false)] out T parsedValue);

        public static bool TryReadSetting<T>(this IExpressChain chain, string setting, TryParse<T> tryParse, [MaybeNullWhen(false)] out T value)
        {   
            if (chain.Settings.TryGetValue(setting, out var stringValue)
                && tryParse(stringValue, out var result))
            {
                value = result;
                return true;
            }

            value = default;
            return false;
        }

        public static bool IsRunning(this IExpressChain @this)
        {
            for (var i = 0; i < @this.ConsensusNodes.Count; i++)
            {
                if (@this.ConsensusNodes[i].IsRunning())
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsRunning(this ExpressConsensusNode node)
        {
            // Check to see if there's a neo-express blockchain currently running by
            // attempting to open a mutex with the multisig account address for a name

            var account = node.Wallet.Accounts.Single(a => a.IsDefault);
            return System.Threading.Mutex.TryOpenExisting(
                ExpressSystem.GLOBAL_PREFIX + account.ScriptHash,
                out var _);
        }

        public static (Neo.Wallets.Wallet wallet, UInt160 accountHash) ResolveSigner(this IExpressChain @this, string name, string password)
            => @this.TryResolveSigner(name, password, out var wallet, out var accountHash)
                ? (wallet, accountHash)
                : throw new Exception("ResolveSigner Failed");


        public static UInt160 ResolveAccountHash(this IExpressChain chain, string name)
            => chain.TryResolveAccountHash(name, out var hash) 
                ? hash 
                : throw new Exception("ResolveAccountHash failed");

        public static bool TryResolveAccountHash(this IExpressChain chain, string name, out UInt160 accountHash)
        {
            if (chain.Wallets != null && chain.Wallets.Count > 0)
            {
                for (int i = 0; i < chain.Wallets.Count; i++)
                {
                    if (string.Equals(name, chain.Wallets[i].Name, StringComparison.OrdinalIgnoreCase))
                    {
                        accountHash = chain.Wallets[i].DefaultAccount.GetScriptHash();
                        return true;
                    }
                }
            }

            Debug.Assert(chain.ConsensusNodes != null && chain.ConsensusNodes.Count > 0);
            for (int i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var nodeWallet = chain.ConsensusNodes[i].Wallet;
                if (string.Equals(name, nodeWallet.Name, StringComparison.OrdinalIgnoreCase))
                {
                    accountHash = nodeWallet.DefaultAccount.GetScriptHash();
                    return true;
                }
            }

            if (IExpressChain.GENESIS.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                accountHash = chain.GetConsensusScriptHash();
                return true;
            }

            if (UInt160.TryParse(name, out accountHash))
            {
                return true;
            }

            try
            {
                accountHash = Neo.Wallets.Helper.ToScriptHash(name, chain.AddressVersion);
                return true;
            }
            catch {}

            accountHash = UInt160.Zero;
            return false;
        }

    }
}
