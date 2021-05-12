using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.Wallets;
using NeoExpress.Models;

namespace NeoExpress
{
    static class ExpressChainExtensions
    {
        public static IReadOnlyList<Wallet> GetMultiSigWallets(this ExpressChain chain, ProtocolSettings settings, UInt160 accountHash)
        {
            var builder = ImmutableList.CreateBuilder<Wallet>();
            for (int i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var wallet = DevWallet.FromExpressWallet(settings, chain.ConsensusNodes[i].Wallet);
                if (wallet.GetAccount(accountHash) != null) builder.Add(wallet);
            }

            for (int i = 0; i < chain.Wallets.Count; i++)
            {
                var wallet = DevWallet.FromExpressWallet(settings, chain.Wallets[i]);
                if (wallet.GetAccount(accountHash) != null) builder.Add(wallet);
            }

            return builder.ToImmutable();
        }

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

        public static string GetPassword(this ExpressChain chain, string name, string password)
        {
            // if the user specified a password, use it
            if (!string.IsNullOrEmpty(password)) return password;

            // if the name is a valid Neo Express account name, no password is needed
            if (chain.IsReservedName(name)) return password;
            if (chain.Wallets.Any(w => name.Equals(w.Name, StringComparison.OrdinalIgnoreCase))) return password;

            // if a password is needed but not provided, prompt the user
            return McMaster.Extensions.CommandLineUtils.Prompt.GetPassword($"enter password for {name}");
        }

        public static (Wallet wallet, WalletAccount account) GetGenesisAccount(this ExpressChain chain, ProtocolSettings? settings = null)
        {
            Debug.Assert(chain.ConsensusNodes != null && chain.ConsensusNodes.Count > 0);

            settings ??= chain.GetProtocolSettings();
            var wallet = DevWallet.FromExpressWallet(settings, chain.ConsensusNodes[0].Wallet);
            var account = wallet.GetMultiSigAccounts().Single();
            return (wallet, account);
        }

        public static bool TryGetAccountHash(this ExpressChain chain, string name, [MaybeNullWhen(false)] out UInt160 accountHash, ProtocolSettings? settings = null)
        {
            if (chain.ConsensusNodes.Count < 1) throw new FormatException("Invalid Express Chain");
            settings ??= chain.GetProtocolSettings();

            if (GENESIS.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                var account = chain.ConsensusNodes[0].Wallet.DefaultAccount;
                if (account == null) throw new FormatException("Consensus Node 0 Wallet missing default account");
                accountHash = account.ToScriptHash(settings.AddressVersion);
                return true;
            }

            if (chain.Wallets != null)
            {
                for (int i = 0; i < chain.Wallets.Count; i++)
                {
                    if (name.Equals(chain.Wallets[i].Name, StringComparison.OrdinalIgnoreCase))
                    {
                        var account = chain.Wallets[i].Accounts.Single();
                        accountHash = account.ToScriptHash(settings.AddressVersion);
                        return true;
                    }
                }
            }

            for (int i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var nodeWallet = chain.ConsensusNodes[i].Wallet;
                if (string.Equals(name, nodeWallet.Name, StringComparison.OrdinalIgnoreCase))
                {
                    var account = nodeWallet.DefaultAccount;
                    if (account == null) throw new FormatException($"Consensus Node {i} Wallet missing default account");
                    accountHash = account.ToScriptHash(settings.AddressVersion);
                    return true;
                }
            }

            try
            {
                accountHash = name.ToScriptHash(settings.AddressVersion);
                return true;
            }
            catch (FormatException)
            {
                // format exception means the means the provided name string isn't a valid address string
            }

            accountHash = UInt160.Zero;
            return false;
        }

        public static bool TryGetAccount(this ExpressChain chain, string name, [MaybeNullWhen(false)] out Wallet wallet, [MaybeNullWhen(false)] out WalletAccount account, ProtocolSettings? settings = null)
        {
            settings ??= chain.GetProtocolSettings();

            if (chain.Wallets != null && chain.Wallets.Count > 0)
            {
                for (int i = 0; i < chain.Wallets.Count; i++)
                {
                    if (string.Equals(name, chain.Wallets[i].Name, StringComparison.OrdinalIgnoreCase))
                    {
                        wallet = DevWallet.FromExpressWallet(settings, chain.Wallets[i]);
                        account = wallet.GetAccounts().Single(a => a.IsDefault);
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
                    wallet = DevWallet.FromExpressWallet(settings, nodeWallet);
                    account = wallet.GetAccounts().Single(a => a.IsDefault);
                    return true;
                }
            }

            if (GENESIS.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                (wallet, account) = chain.GetGenesisAccount();
                return true;
            }

            wallet = null!;
            account = null!;
            return false;
        }

        class ExpressWalletAccountEqualityComparer : EqualityComparer<ExpressWalletAccount>
        {
            public readonly static ExpressWalletAccountEqualityComparer Instance = new ExpressWalletAccountEqualityComparer();

            private ExpressWalletAccountEqualityComparer() { }

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

        public delegate bool TryParse<T>(string value, [MaybeNullWhen(false)] out T parsedValue);

        public static bool TryReadSetting<T>(this ExpressChain chain, string setting, TryParse<T> tryParse, [MaybeNullWhen(false)] out T value)
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
    }
}
