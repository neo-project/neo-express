using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Neo.SmartContract;
using Neo.Wallets;
using NeoExpress.Models;
using Nito.Disposables;

namespace NeoExpress
{

    static class ExpressChainExtensions
    {
        public static IExpressNode GetExpressNode(this Neo.BlockchainToolkit.Models.ExpressChain chain, IFileSystem fileSystem, bool offlineTrace = false)
        {
            throw new NotImplementedException();
        }


        public static Contract CreateGenesisContract(this Neo.BlockchainToolkit.Models.ExpressChain chain)
        {
            List<Neo.Cryptography.ECC.ECPoint> publicKeys = new(chain.ConsensusNodes.Count);
            foreach (var node in chain.ConsensusNodes)
            {
                var account = node.Wallet.DefaultAccount ?? throw new Exception("Missing Default Account");
                var privateKey = Convert.FromHexString(account.PrivateKey);
                var keyPair = new KeyPair(privateKey);
                publicKeys.Add(keyPair.PublicKey);
            }

            var m = publicKeys.Count * 2 / 3 + 1;
            return Contract.CreateMultiSigContract(m, publicKeys);
        }

        public static UInt160 GetGenesisScriptHash(this Neo.BlockchainToolkit.Models.ExpressChain chain)
        {
            var contract = CreateGenesisContract(chain);
            return contract.ScriptHash;
        }

        public static UInt160 ResolveAccountHash(this Neo.BlockchainToolkit.Models.ExpressChain chain, string name)
            => chain.TryResolveAccountHash(name, out var hash) 
                ? hash 
                : throw new Exception("ResolveAccountHash failed");

        public static bool TryResolveAccountHash(this Neo.BlockchainToolkit.Models.ExpressChain chain, string name, out UInt160 accountHash)
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

            if (GENESIS.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                var contract = chain.CreateGenesisContract();
                accountHash = contract.ScriptHash;
                return true;
            }

            if (UInt160.TryParse(name, out accountHash))
            {
                return true;
            }

            try
            {
                accountHash = name.ToScriptHash(chain.AddressVersion);
                return true;
            }
            catch {}

            accountHash = UInt160.Zero;
            return false;
        }

        public static UInt160 GetScriptHash(this ExpressWalletAccount? @this)
        {
            ArgumentNullException.ThrowIfNull(@this);

            var keyPair = new KeyPair(@this.PrivateKey.HexToBytes());
            var contract = Neo.SmartContract.Contract.CreateSignatureContract(keyPair.PublicKey);
            return contract.ScriptHash;
        }








        public static bool IsMultiSigContract(this ExpressWalletAccount @this)
            => Neo.SmartContract.Helper.IsMultiSigContract(Convert.FromHexString(@this.Contract.Script));

        public static bool IsMultiSigContract(this WalletAccount @this)
            => Neo.SmartContract.Helper.IsMultiSigContract(@this.Contract.Script);

        public static IEnumerable<WalletAccount> GetMultiSigAccounts(this Wallet wallet) => wallet.GetAccounts().Where(IsMultiSigContract);

        public static IReadOnlyList<Wallet> GetMultiSigWallets(this Neo.BlockchainToolkit.Models.ExpressChain chain, ProtocolSettings settings, UInt160 accountHash)
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

        public static KeyPair[] GetConsensusNodeKeys(this Neo.BlockchainToolkit.Models.ExpressChain chain)
            => chain.ConsensusNodes
                .Select(n => n.Wallet.DefaultAccount ?? throw new Exception($"{n.Wallet.Name} missing default account"))
                .Select(a => new KeyPair(a.PrivateKey.HexToBytes()))
                .ToArray();

        internal const string GENESIS = "genesis";

        public static bool IsReservedName(this Neo.BlockchainToolkit.Models.ExpressChain chain, string name)
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

        // public static string ResolvePassword(this IExpressFile expressFile, string name, string password)
        // {
        //     // TODO: expressFile.Chain.ResolvePassword
        //     return expressFile.Chain.ResolvePassword(name, password);
        // }

        public static string ResolvePassword(this Neo.BlockchainToolkit.Models.ExpressChain chain, string name, string password)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException($"{nameof(name)} parameter can't be null or empty", nameof(name));

            // if the user specified a password, use it
            if (!string.IsNullOrEmpty(password)) return password;

            // if the name is a valid Neo Express account name, no password is needed
            if (chain.IsReservedName(name)) return string.Empty;
            if (chain.Wallets.Any(w => name.Equals(w.Name, StringComparison.OrdinalIgnoreCase))) return string.Empty;

            // if a password is needed but not provided, prompt the user
            return McMaster.Extensions.CommandLineUtils.Prompt.GetPassword($"enter password for {name}");
        }






        public static (Wallet wallet, UInt160 accountHash) GetGenesisAccount(this Neo.BlockchainToolkit.Models.ExpressChain chain, ProtocolSettings settings)
        {
            Debug.Assert(chain.ConsensusNodes != null && chain.ConsensusNodes.Count > 0);

            var wallet = DevWallet.FromExpressWallet(settings, chain.ConsensusNodes[0].Wallet);
            var account = wallet.GetMultiSigAccounts().Single();
            return (wallet, account.ScriptHash);
        }

        public static ExpressWallet? GetWallet(this Neo.BlockchainToolkit.Models.ExpressChain chain, string name)
            => (chain.Wallets ?? Enumerable.Empty<ExpressWallet>())
                .SingleOrDefault(w => string.Equals(name, w.Name, StringComparison.OrdinalIgnoreCase));



        public static bool TryGetSigningAccount(this Neo.BlockchainToolkit.Models.ExpressChain chain, string name, string password, IFileSystem fileSystem, [MaybeNullWhen(false)] out Wallet wallet, [MaybeNullWhen(false)] out UInt160 accountHash)
        {
            if (!string.IsNullOrEmpty(name))
            {
                var settings = chain.GetProtocolSettings();

                if (name.Equals(ExpressChainExtensions.GENESIS, StringComparison.OrdinalIgnoreCase))
                {
                    (wallet, accountHash) = chain.GetGenesisAccount(settings);
                    return true;
                }

                if (chain.Wallets != null && chain.Wallets.Count > 0)
                {
                    for (int i = 0; i < chain.Wallets.Count; i++)
                    {
                        var expWallet = chain.Wallets[i];
                        if (name.Equals(expWallet.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            wallet = DevWallet.FromExpressWallet(settings, expWallet);
                            accountHash = wallet.GetAccounts().Single(a => a.IsDefault).ScriptHash;
                            return true;
                        }
                    }
                }

                for (int i = 0; i < chain.ConsensusNodes.Count; i++)
                {
                    var expWallet = chain.ConsensusNodes[i].Wallet;
                    if (name.Equals(expWallet.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        wallet = DevWallet.FromExpressWallet(settings, expWallet);
                        accountHash = wallet.GetAccounts().Single(a => !a.Contract.Script.IsMultiSigContract()).ScriptHash;
                        return true;
                    }
                }

                if (!string.IsNullOrEmpty(password))
                {
                    if (TryGetNEP2Wallet(name, password, settings, out var devWallet)
                        || fileSystem.TryImportNEP6(name, password, settings, out devWallet))
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
            accountHash = null;
            return false;

            static bool TryGetNEP2Wallet(string nep2, string password, ProtocolSettings settings, [MaybeNullWhen(false)] out DevWallet wallet)
            {
                try
                {
                    var privateKey = Wallet.GetPrivateKeyFromNEP2(nep2, password, settings.AddressVersion);
                    wallet = new DevWallet(settings, string.Empty);
                    var account = wallet.CreateAccount(privateKey);
                    account.IsDefault = true;
                    return true;
                }
                catch
                {
                    wallet = null;
                    return false;
                }
            }
        }
    }
}
