// Copyright (C) 2015-2024 The Neo Project.
//
// ExpressChainExtensions.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.Wallets;
using NeoExpress.Models;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

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
                if (wallet.GetAccount(accountHash) is not null)
                    builder.Add(wallet);
            }

            for (int i = 0; i < chain.Wallets.Count; i++)
            {
                var wallet = DevWallet.FromExpressWallet(settings, chain.Wallets[i]);
                if (wallet.GetAccount(accountHash) is not null)
                    builder.Add(wallet);
            }

            return builder.ToImmutable();
        }

        public static KeyPair[] GetConsensusNodeKeys(this ExpressChain chain)
            => chain.ConsensusNodes
                .Select(n => n.Wallet.DefaultAccount ?? throw new Exception($"{n.Wallet.Name} missing default account"))
                .Select(a => new KeyPair(a.PrivateKey.HexToBytes()))
                .ToArray();

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
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException($"{nameof(name)} parameter can't be null or empty", nameof(name));

            // if the user specified a password, use it
            if (!string.IsNullOrEmpty(password))
                return password;

            // if the name is a valid Neo Express account name, no password is needed
            if (chain.IsReservedName(name))
                return password;
            if (chain.Wallets.Any(w => name.Equals(w.Name, StringComparison.OrdinalIgnoreCase)))
                return password;

            // if a password is needed but not provided, prompt the user
            return McMaster.Extensions.CommandLineUtils.Prompt.GetPassword($"enter password for {name}");
        }

        public static UInt160 GetScriptHash(this ExpressWalletAccount? @this)
        {
            ArgumentNullException.ThrowIfNull(@this);

            var keyPair = new KeyPair(@this.PrivateKey.HexToBytes());
            var contract = Neo.SmartContract.Contract.CreateSignatureContract(keyPair.PublicKey);
            return contract.ScriptHash;
        }

        public static bool TryGetAccountHash(this ExpressChain chain, string name, [MaybeNullWhen(false)] out UInt160 accountHash)
        {
            if (chain.Wallets is not null && chain.Wallets.Count > 0)
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

            Debug.Assert(chain.ConsensusNodes is not null && chain.ConsensusNodes.Count > 0);

            for (int i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var nodeWallet = chain.ConsensusNodes[i].Wallet;
                if (string.Equals(name, nodeWallet.Name, StringComparison.OrdinalIgnoreCase))
                {
                    accountHash = nodeWallet.DefaultAccount.GetScriptHash();
                    return true;
                }
            }

            if (GENESIS.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                var keys = chain.ConsensusNodes
                    .Select(n => n.Wallet.DefaultAccount ?? throw new Exception())
                    .Select(a => new KeyPair(a.PrivateKey.HexToBytes()).PublicKey)
                    .ToArray();
                var contract = Neo.SmartContract.Contract.CreateMultiSigContract((keys.Length * 2 / 3) + 1, keys);
                accountHash = contract.ScriptHash;
                return true;
            }

            return chain.TryParseScriptHash(name, out accountHash);
        }

        public static bool TryParseScriptHash(this ExpressChain chain, string name, [MaybeNullWhen(false)] out UInt160 hash)
        {
            try
            {
                hash = name.ToScriptHash(chain.AddressVersion);
                return true;
            }
            catch
            {
                hash = null;
                return false;
            }
        }

        public static (Wallet wallet, UInt160 accountHash) GetGenesisAccount(this ExpressChain chain, ProtocolSettings settings)
        {
            Debug.Assert(chain.ConsensusNodes is not null && chain.ConsensusNodes.Count > 0);

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
