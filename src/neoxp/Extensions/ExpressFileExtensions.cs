using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Neo;
using Neo.BlockchainToolkit.Models;
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

    }
}
