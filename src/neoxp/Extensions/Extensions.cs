using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.BlockchainToolkit;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using NeoExpress.Models;

namespace NeoExpress
{
    static class Extensions
    {
        public static void WriteException(this CommandLineApplication app, Exception exception, bool showInnerExceptions = false)
        {
            var showStackTrace = ((CommandOption<bool>)app.GetOptions().Single(o => o.LongName == "stack-trace")).ParsedValue;

            app.Error.WriteLine($"\x1b[1m\x1b[31m\x1b[40m{exception.GetType()}: {exception.Message}\x1b[0m");

            if (showStackTrace) app.Error.WriteLine($"\x1b[1m\x1b[37m\x1b[40m{exception.StackTrace}\x1b[0m");

            if (showInnerExceptions || showStackTrace)
            {
                while (exception.InnerException != null)
                {
                    app.Error.WriteLine($"\x1b[1m\x1b[33m\x1b[40m\tInner {exception.InnerException.GetType().Name}: {exception.InnerException.Message}\x1b[0m");
                    exception = exception.InnerException;
                }
            }
        }

        public static bool IsMultiSigContract(this WalletAccount @this) => @this.Contract.Script.IsMultiSigContract();

        public static IEnumerable<WalletAccount> GetMultiSigAccounts(this Wallet wallet) => wallet.GetAccounts().Where(IsMultiSigContract);

        public static ApplicationEngine Invoke(this Neo.VM.ScriptBuilder builder, ProtocolSettings settings, DataCache snapshot, IVerifiable? container = null)
            => Invoke(builder.ToArray(), settings, snapshot, container);

        public static ApplicationEngine Invoke(this Neo.VM.Script script, ProtocolSettings settings, DataCache snapshot, IVerifiable? container = null)
            => ApplicationEngine.Run(
                script: script,
                snapshot: snapshot,
                settings: settings,
                container: container);

        public static async Task WriteTxHashAsync(this TextWriter writer, UInt256 txHash, string txType = "", bool json = false)
        {
            if (json)
            {
                await writer.WriteLineAsync($"{txHash}").ConfigureAwait(false);
            }
            else
            {
                if (!string.IsNullOrEmpty(txType)) await writer.WriteAsync($"{txType} ").ConfigureAwait(false);
                await writer.WriteLineAsync($"Transaction {txHash} submitted").ConfigureAwait(false);
            }
        }

        public static BigDecimal ToBigDecimal(this RpcNep17Balance balance, byte decimals)
            => new BigDecimal(balance.Amount, decimals);

        public static string ToHexString(this byte[] value, bool reverse = false)
        {
            var sb = new System.Text.StringBuilder();

            if (reverse)
            {
                for (int i = value.Length - 1; i >= 0; i--)
                {
                    sb.AppendFormat("{0:x2}", value[i]);
                }
            }
            else
            {
                for (int i = 0; i < value.Length; i++)
                {
                    sb.AppendFormat("{0:x2}", value[i]);
                }
            }
            return sb.ToString();
        }

        public static byte[] ToByteArray(this string value)
        {
            if (value == null || value.Length == 0)
                return new byte[0];
            if (value.Length % 2 == 1)
                throw new FormatException();
            byte[] result = new byte[value.Length / 2];
            for (int i = 0; i < result.Length; i++)
                result[i] = byte.Parse(value.Substring(i * 2, 2), System.Globalization.NumberStyles.AllowHexSpecifier);
            return result;
        }

        public static Task<PolicyValues> GetPolicyAsync(this RpcClient rpcClient)
        {
            return ExpressNodeExtensions.GetPolicyAsync(script => rpcClient.InvokeScriptAsync(script));
        }

        public static bool TryGetSigningAccount(this ExpressChainManager chainManager, string name, string password, [MaybeNullWhen(false)] out Wallet wallet, [MaybeNullWhen(false)] out UInt160 accountHash)
        {
            if (!string.IsNullOrEmpty(name))
            {
                var settings = chainManager.Chain.GetProtocolSettings();

                if (name.Equals(ExpressChainExtensions.GENESIS, StringComparison.OrdinalIgnoreCase))
                {
                    (wallet, accountHash) = chainManager.Chain.GetGenesisAccount(settings);
                    return true;
                }

                if (chainManager.Chain.Wallets != null && chainManager.Chain.Wallets.Count > 0)
                {
                    for (int i = 0; i < chainManager.Chain.Wallets.Count; i++)
                    {
                        var expWallet = chainManager.Chain.Wallets[i];
                        if (name.Equals(expWallet.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            wallet = DevWallet.FromExpressWallet(settings, expWallet);
                            accountHash = wallet.GetAccounts().Single(a => a.IsDefault).ScriptHash;
                            return true;
                        }
                    }
                }

                for (int i = 0; i < chainManager.Chain.ConsensusNodes.Count; i++)
                {
                    var expWallet = chainManager.Chain.ConsensusNodes[i].Wallet;
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

        public static IEnumerable<TokenContract> EnumerateTokenContracts(this NeoSystem neoSystem)
        {
            using var snapshot = neoSystem.GetSnapshot();
            return snapshot.EnumerateTokenContracts(neoSystem.Settings);
        }

        public static IEnumerable<TokenContract> EnumerateTokenContracts(this DataCache snapshot, ProtocolSettings settings)
        {
            foreach (var (contractHash, standard) in TokenContract.Enumerate(snapshot))
            {
                if (TryLoadTokenInfo(contractHash, snapshot, settings, out var info))
                {
                    yield return new TokenContract(info.symbol, info.decimals, contractHash, standard);
                }
            }

            static bool TryLoadTokenInfo(UInt160 scriptHash, DataCache snapshot, ProtocolSettings settings, out (string symbol, byte decimals) info)
            {
                if (scriptHash == NativeContract.NEO.Hash)
                {
                    info = (NativeContract.NEO.Symbol, NativeContract.NEO.Decimals);
                    return true;
                }

                if (scriptHash == NativeContract.GAS.Hash)
                {
                    info = (NativeContract.GAS.Symbol, NativeContract.GAS.Decimals);
                    return true;
                }

                using var builder = new ScriptBuilder();
                builder.EmitDynamicCall(scriptHash, "symbol");
                builder.EmitDynamicCall(scriptHash, "decimals");
                using var engine = builder.Invoke(settings, snapshot);
                if (engine.State != VMState.FAULT && engine.ResultStack.Count == 2)
                {
                    var decimals = (byte)engine.ResultStack.Pop().GetInteger();
                    var symbol = engine.ResultStack.Pop().GetString();
                    if (symbol != null)
                    {
                        info = (symbol, decimals);
                        return true;
                    }
                }

                info = default;
                return false;
            }
        }

        public static IEnumerable<(TokenContract contract, BigInteger balance)> ListNep17Balances(this NeoSystem neoSystem, UInt160 address)
        {
            using var snapshot = neoSystem.GetSnapshot();
            return snapshot.ListNep17Balances(address, neoSystem.Settings);
        }

        public static IEnumerable<(TokenContract contract, BigInteger balance)> ListNep17Balances(this DataCache snapshot, UInt160 address, ProtocolSettings settings)
        {
            var contracts = TokenContract.Enumerate(snapshot)
                .Where(c => c.standard == TokenStandard.Nep17);

            var addressArray = address.ToArray();
            var contractCount = 0;
            using var builder = new ScriptBuilder();
            foreach (var c in contracts.Reverse())
            {
                builder.EmitDynamicCall(c.scriptHash, "symbol");
                builder.EmitDynamicCall(c.scriptHash, "decimals");
                builder.EmitDynamicCall(c.scriptHash, "balanceOf", addressArray);
                contractCount++;
            }

            var engine = builder.Invoke(settings, snapshot);
            if (engine.State != VMState.FAULT && engine.ResultStack.Count == contractCount * 3)
            {
                var resultStack = engine.ResultStack;
                for (var i = 0; i < contractCount; i++)
                {
                    var index = i * 3;
                    var symbol = resultStack.Peek(index + 2).GetString();
                    if (symbol == null) continue;
                    var decimals = (byte)resultStack.Peek(index + 1).GetInteger();
                    var balance = resultStack.Peek(index).GetInteger();
                    var (scriptHash, standard) = contracts.ElementAt(i);
                    yield return (new TokenContract(symbol, decimals, scriptHash, standard), balance);
                }
            }
        }
    }
}
