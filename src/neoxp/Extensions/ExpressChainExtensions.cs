using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract;
using Neo.Wallets;
using NeoExpress.Models;
using NeoExpress.Node;

namespace NeoExpress
{
    static class ExpressChainExtensions
    {
        public static ProtocolSettings GetProtocolSettings(this IExpressChain? chain, uint? secondsPerBlock = null)
        {
            var blockTime = secondsPerBlock.HasValue
                ? secondsPerBlock.Value
                : (chain?.TryReadSetting("chain.SecondsPerBlock", uint.TryParse, out uint setting) ?? false)
                    ? setting
                    : 0;

            return chain == null
                ? ProtocolSettings.Default
                : ProtocolSettings.Default with
                {
                    Network = chain.Network,
                    AddressVersion = chain.AddressVersion,
                    MillisecondsPerBlock = blockTime == 0 ? 15000 : blockTime * 1000,
                    ValidatorsCount = chain.ConsensusNodes.Count,
                    StandbyCommittee = chain.ConsensusNodes.Select(GetPublicKey).ToArray(),
                    SeedList = chain.ConsensusNodes
                        .Select(n => $"{System.Net.IPAddress.Loopback}:{n.TcpPort}")
                        .ToArray(),
                };

            static Neo.Cryptography.ECC.ECPoint GetPublicKey(ExpressConsensusNode node)
                => new Neo.Wallets.KeyPair(Convert.FromHexString(node.Wallet.Accounts.Select(a => a.PrivateKey).Distinct().Single())).PublicKey;
        }

        public static Contract GetConsensusContract(this IExpressChain chain)
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

        public static ExpressWallet? GetWallet(this IExpressChain chain, string name)
            => (chain.Wallets ?? Enumerable.Empty<ExpressWallet>())
                .SingleOrDefault(w => string.Equals(name, w.Name, StringComparison.OrdinalIgnoreCase));

        public static IReadOnlyList<Wallet> GetMultiSigWallets(this IExpressChain chain, UInt160 accountHash)
        {
            var wallets = new List<Wallet>();
            var settings = chain.GetProtocolSettings();

            for (int i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var wallet = DevWallet.FromExpressWallet(chain.ConsensusNodes[i].Wallet, settings);
                if (wallet.GetAccount(accountHash) is not null) wallets.Add(wallet);
            }

            for (int i = 0; i < chain.Wallets.Count; i++)
            {
                var wallet = DevWallet.FromExpressWallet(chain.Wallets[i], settings);
                if (wallet.GetAccount(accountHash) is not null) wallets.Add(wallet);
            }

            return wallets;
        }

        public static UInt160 ResolveAccountHash(this IExpressChain chain, string name)
            => chain.TryResolveAccountHash(name, out var hash)
                ? hash
                : throw new Exception("TryResolveAccountHash failed");

        public static bool TryResolveAccountHash(this IExpressChain chain, string name, out UInt160 accountHash)
        {
            if (chain.Wallets != null && chain.Wallets.Count > 0)
            {
                for (int i = 0; i < chain.Wallets.Count; i++)
                {
                    if (string.Equals(name, chain.Wallets[i].Name, StringComparison.OrdinalIgnoreCase))
                    {
                        accountHash = chain.Wallets[i].DefaultAccount.CalculateScriptHash();
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
                    accountHash = nodeWallet.DefaultAccount.CalculateScriptHash();
                    return true;
                }
            }

            if (IExpressChain.GENESIS.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                accountHash = chain.GetConsensusContract().ScriptHash;
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
            catch { }

            accountHash = UInt160.Zero;
            return false;
        }

        public static string ResolvePassword(this IExpressChain chain, string name, string password)
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

        public static (Neo.Wallets.Wallet wallet, UInt160 accountHash) ResolveSigner(this IExpressChain @this, string name, string password)
            => @this.TryResolveSigner(name, password, out var wallet, out var accountHash)
                ? (wallet, accountHash)
                : throw new Exception("ResolveSigner Failed");

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

        public static bool IsMultiSig(this WalletAccount @this) => Neo.SmartContract.Helper.IsMultiSigContract(@this.Contract.Script);

        public static Neo.IO.Json.JObject ExportNEP6(this ExpressWallet @this, string password, ProtocolSettings settings)
        {
            var nep6Wallet = new Neo.Wallets.NEP6.NEP6Wallet(string.Empty, password, settings, @this.Name);
            foreach (var account in @this.Accounts)
            {
                var key = Convert.FromHexString(account.PrivateKey);
                var nep6Account = nep6Wallet.CreateAccount(key);
                nep6Account.Label = account.Label;
                nep6Account.IsDefault = account.IsDefault;
            }
            return nep6Wallet.ToJson();
        }

        public static UInt160 CalculateScriptHash(this ExpressWalletAccount? @this)
        {
            ArgumentNullException.ThrowIfNull(@this);

            var keyPair = new KeyPair(@this.PrivateKey.HexToBytes());
            var contract = Neo.SmartContract.Contract.CreateSignatureContract(keyPair.PublicKey);
            return contract.ScriptHash;
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
    }
}
