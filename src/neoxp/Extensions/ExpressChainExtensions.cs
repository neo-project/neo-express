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
        public static bool IsMultiSigContract(this WalletAccount @this) => @this.Contract.Script.IsMultiSigContract();

        public static IEnumerable<WalletAccount> GetMultiSigAccounts(this Wallet wallet) => wallet.GetAccounts().Where(IsMultiSigContract);


        public static bool IsRunning(this ExpressChain chain)
        {
            for (var i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                if (chain.ConsensusNodes[i].IsRunning())
                {
                    return true;
                }
            }
            return false;
        }

        public static async Task<(string path, IExpressNode.CheckpointMode checkpointMode)> 
            CreateCheckpointAsync(this ExpressChain chain, IFileSystem fileSystem, 
                IExpressNode expressNode, string checkpointPath, bool force, 
                System.IO.TextWriter? writer = null)
        {
            if (chain.ConsensusNodes.Count != 1)
            {
                throw new ArgumentException("Checkpoint create is only supported on single node express instances", nameof(chain));
            }

            checkpointPath = fileSystem.ResolveCheckpointFileName(checkpointPath);
            if (fileSystem.File.Exists(checkpointPath))
            {
                if (force)
                {
                    fileSystem.File.Delete(checkpointPath);
                }
                else
                {
                    throw new Exception("You must specify --force to overwrite an existing file");
                }
            }

            var parentPath = fileSystem.Path.GetDirectoryName(checkpointPath);
            if (!fileSystem.Directory.Exists(parentPath))
            {
                fileSystem.Directory.CreateDirectory(parentPath);
            }

            var mode = await expressNode.CreateCheckpointAsync(checkpointPath).ConfigureAwait(false);

            if (writer != null)
            {
                await writer.WriteLineAsync($"Created {fileSystem.Path.GetFileName(checkpointPath)} checkpoint {mode}").ConfigureAwait(false);
            }

            return (checkpointPath, mode);
        }

        internal static void RestoreCheckpoint(this ExpressChain chain, IFileSystem fileSystem,
            string checkPointArchive, bool force)
        {
            if (chain.ConsensusNodes.Count != 1)
            {
                throw new ArgumentException("Checkpoint restore is only supported on single node express instances", nameof(chain));
            }

            checkPointArchive = fileSystem.ResolveCheckpointFileName(checkPointArchive);
            if (!fileSystem.File.Exists(checkPointArchive))
            {
                throw new Exception($"Checkpoint {checkPointArchive} couldn't be found");
            }

            var node = chain.ConsensusNodes[0];
            if (node.IsRunning())
            {
                var scriptHash = node.Wallet.DefaultAccount?.ScriptHash ?? "<unknown>";
                throw new InvalidOperationException($"node {scriptHash} currently running");
            }

            var checkpointTempPath = fileSystem.GetTempFolder();
            using var folderCleanup = AnonymousDisposable.Create(() =>
            {
                if (fileSystem.Directory.Exists(checkpointTempPath))
                {
                    fileSystem.Directory.Delete(checkpointTempPath, true);
                }
            });

            var nodePath = fileSystem.GetNodePath(node);
            if (fileSystem.Directory.Exists(nodePath))
            {
                if (!force)
                {
                    throw new Exception("You must specify force to restore a checkpoint to an existing blockchain.");
                }

                fileSystem.Directory.Delete(nodePath, true);
            }

            var settings = chain.GetProtocolSettings();
            var wallet = DevWallet.FromExpressWallet(settings, node.Wallet);
            var multiSigAccount = wallet.GetMultiSigAccounts().Single();
            RocksDbUtility.RestoreCheckpoint(checkPointArchive, checkpointTempPath,
                settings.Network, settings.AddressVersion, multiSigAccount.ScriptHash);
            fileSystem.Directory.Move(checkpointTempPath, nodePath);
        }

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
            if (string.IsNullOrEmpty(name)) throw new ArgumentException($"{nameof(name)} parameter can't be null or empty", nameof(name));

            // if the user specified a password, use it
            if (!string.IsNullOrEmpty(password)) return password;

            // if the name is a valid Neo Express account name, no password is needed
            if (chain.IsReservedName(name)) return password;
            if (chain.Wallets.Any(w => name.Equals(w.Name, StringComparison.OrdinalIgnoreCase))) return password;

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

        public static bool TryGetSigningAccount(this ExpressChain chain, string name, string password, [MaybeNullWhen(false)] out Wallet wallet, [MaybeNullWhen(false)] out UInt160 accountHash)
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
                    if (TryGetNEP2Wallet(name, password, settings, out wallet, out accountHash))
                    {
                        return true;
                    }

                    if (TryGetNEP6Wallet(name, password, settings, out wallet, out accountHash))
                    {
                        return true;
                    }
                }
            }

            wallet = null;
            accountHash = null;
            return false;

            static bool TryGetNEP2Wallet(string nep2, string password, ProtocolSettings settings, [MaybeNullWhen(false)] out Wallet wallet, [MaybeNullWhen(false)] out UInt160 accountHash)
            {
                try
                {
                    var privateKey = Wallet.GetPrivateKeyFromNEP2(nep2, password, settings.AddressVersion);
                    wallet = new DevWallet(settings, string.Empty);
                    var account = wallet.CreateAccount(privateKey);
                    accountHash = account.ScriptHash;
                    return true;
                }
                catch
                {
                    wallet = null;
                    accountHash = null;
                    return false;
                }
            }

            static bool TryGetNEP6Wallet(string path, string password, ProtocolSettings settings, [MaybeNullWhen(false)] out Wallet wallet, [MaybeNullWhen(false)] out UInt160 accountHash)
            {
                try
                {
                    var nep6wallet = new Neo.Wallets.NEP6.NEP6Wallet(path, settings);
                    using var unlock = nep6wallet.Unlock(password);
                    var nep6account = nep6wallet.GetAccounts().SingleOrDefault(a => a.IsDefault)
                        ?? nep6wallet.GetAccounts().SingleOrDefault()
                        ?? throw new InvalidOperationException("Neo-express only supports NEP-6 wallets with a single default account or a single account");
                    if (nep6account.IsMultiSigContract()) throw new Exception("Neo-express doesn't supports multi-sig NEP-6 accounts");
                    var keyPair = nep6account.GetKey() ?? throw new Exception("account.GetKey() returned null");
                    wallet = new DevWallet(settings, string.Empty);
                    var account = wallet.CreateAccount(keyPair.PrivateKey);
                    accountHash = account.ScriptHash;
                    return true;
                }
                catch
                {
                    wallet = null;
                    accountHash = null;
                    return false;
                }
            }
        }
    }
}
