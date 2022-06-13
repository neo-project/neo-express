using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract;
using Neo.Wallets;
using NeoExpress.Models;
using NeoExpress.Node;

using FileMode = System.IO.FileMode;
using FileAccess = System.IO.FileAccess;
using StreamWriter = System.IO.StreamWriter;
using Neo.Wallets.NEP6;

namespace NeoExpress
{
    static class TestableExtensions
    {
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

        public static KeyPair[] GetConsensusNodeKeys(this IExpressChain chain)
            => chain.ConsensusNodes
                .Select(n => n.Wallet.DefaultAccount ?? throw new Exception($"{n.Wallet.Name} missing default account"))
                .Select(a => new KeyPair(a.PrivateKey.HexToBytes()))
                .ToArray();

        public static ExpressWallet? GetWallet(this IExpressChain chain, string name)
            => (chain.Wallets ?? Enumerable.Empty<ExpressWallet>())
                .SingleOrDefault(w => string.Equals(name, w.Name, StringComparison.OrdinalIgnoreCase));

        public static IReadOnlyList<Wallet> GetMultiSigWallets(this IExpressChain chain, UInt160 accountHash)
        {
            var wallets = new List<Wallet>();
            var settings = ProtocolSettings.Default with { AddressVersion = chain.AddressVersion };

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

        public static IReadOnlyList<T> ToReadOnlyList<T>(this IEnumerable<T> @this) => @this.ToList();

        public static int Execute(this CommandLineApplication app, Delegate @delegate)
        {
            if (@delegate.Method.ReturnType != typeof(void)) throw new Exception();

            try
            {
                var @params = BindParameters(@delegate, app);
                @delegate.DynamicInvoke(@params);
                return 0;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                app.WriteException(ex.InnerException);
                return 1;
            }
            catch (Exception ex)
            {
                app.WriteException(ex);
                return 1;
            }
        }

        public static async Task<int> ExecuteAsync(this CommandLineApplication app, Delegate @delegate, CancellationToken token = default)
        {
            if (@delegate.Method.ReturnType != typeof(Task)) throw new Exception();
            if (@delegate.Method.ReturnType.GenericTypeArguments.Length > 0) throw new Exception();

            try
            {
                var @params = BindParameters(@delegate, app);
                var @return = @delegate.DynamicInvoke(@params) ?? throw new Exception();
                await ((Task)@return).ConfigureAwait(false);
                return 0;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                app.WriteException(ex.InnerException);
                return 1;
            }
            catch (Exception ex)
            {
                app.WriteException(ex);
                return 1;
            }
        }

        static object[] BindParameters(Delegate @delegate, IServiceProvider provider, CancellationToken token = default)
        {
            var paramInfos = @delegate.Method.GetParameters() ?? throw new Exception();
            var @params = new object[paramInfos.Length];
            for (int i = 0; i < paramInfos.Length; i++)
            {
                @params[i] = paramInfos[i].ParameterType == typeof(CancellationToken)
                    ? token : provider.GetRequiredService(paramInfos[i].ParameterType);
            }
            return @params;
        }

        public static Neo.IO.Json.JObject ExportNEP6(this ExpressWallet @this, string password, byte addressVersion)
        {
            var settings = ProtocolSettings.Default with { AddressVersion = addressVersion };
            var nep6Wallet = new NEP6Wallet(string.Empty, password, settings, @this.Name);
            foreach (var account in @this.Accounts)
            {
                var key = Convert.FromHexString(account.PrivateKey);
                var nep6Account = nep6Wallet.CreateAccount(key);
                nep6Account.Label = account.Label;
                nep6Account.IsDefault = account.IsDefault;
            }
            return nep6Wallet.ToJson();
        }
    }
}
