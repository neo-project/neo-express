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
        // this method only used in Online/OfflineNode ExecuteAsync
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

        internal const string GENESIS = "genesis";

        public static bool IsReservedName(this ExpressChain chain, string name)
        {
            if (string.Equals(GENESIS, name, StringComparison.OrdinalIgnoreCase))
                return true;

            for (int i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                if (string.Equals(chain.ConsensusNodes[i].Wallet.Name, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static string ResolvePassword(this ExpressChain chain, string name, string password)
        {
            // if the user specified a password, use it
            if (!string.IsNullOrEmpty(password)) return password;

            // if the name is a valid Neo Express account name, no password is needed
            if (chain.IsReservedName(name)) return password;
            if (chain.Wallets.Any(w => name.Equals(w.Name, StringComparison.OrdinalIgnoreCase))) return password;

            // if a password is needed but not provided, prompt the user
            return McMaster.Extensions.CommandLineUtils.Prompt.GetPassword($"enter password for {name}");
        }

        public static bool TryGetAccountHash(this ExpressChain chain, string name, [MaybeNullWhen(false)] out UInt160 accountHash, ProtocolSettings? settings = null)
        {
            settings ??= chain.GetProtocolSettings();

            if (chain.Wallets != null && chain.Wallets.Count > 0)
            {
                for (int i = 0; i < chain.Wallets.Count; i++)
                {
                    if (string.Equals(name, chain.Wallets[i].Name, StringComparison.OrdinalIgnoreCase))
                    {
                        var wallet = DevWallet.FromExpressWallet(settings, chain.Wallets[i]);
                        var account = wallet.GetAccounts().Single(a => a.IsDefault);
                        accountHash = account.ScriptHash;
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
                    var wallet = DevWallet.FromExpressWallet(settings, nodeWallet);
                    var account = wallet.GetAccounts().Single(a => a.IsDefault);
                    accountHash = account.ScriptHash;
                    return true;
                }
            }

            if (GENESIS.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                (_, accountHash) = chain.GetGenesisAccount(settings);
                return true;
            }

            if (TryToScriptHash(name, settings.AddressVersion, out accountHash))
            {
                return true;
            }

            accountHash = default;
            return false;

            static bool TryToScriptHash(string name, byte version, [MaybeNullWhen(false)] out UInt160 hash)
            {
                try
                {
                    hash = name.ToScriptHash(version);
                    return true;
                }
                catch
                {
                    hash = null;
                    return false;
                }
            }
        }

        public static (Wallet wallet, UInt160 accountHash) GetGenesisAccount(this ExpressChain chain, ProtocolSettings settings)
        {
            Debug.Assert(chain.ConsensusNodes != null && chain.ConsensusNodes.Count > 0);

            var wallet = DevWallet.FromExpressWallet(settings, chain.ConsensusNodes[0].Wallet);
            var account = wallet.GetMultiSigAccounts().Single();
            return (wallet, account.ScriptHash);
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
