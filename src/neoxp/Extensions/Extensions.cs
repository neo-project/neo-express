﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.BlockchainToolkit;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.Wallets;
using NeoExpress.Models;

namespace NeoExpress
{
    static class Extensions
    {
        public static string Resolve(this System.IO.Abstractions.IDirectoryInfo @this, string path)
            => @this.FileSystem.Path.IsPathFullyQualified(path)
                ? path
                : @this.FileSystem.Path.Combine(@this.FullName, path);

        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> valueFactory)
        {
            if (!dictionary.TryGetValue(key, out var value))
            {
                value = valueFactory(key);
                dictionary[key] = value;
            }

            return value;
        }

        public static void WriteJson(this IConsole console, Neo.IO.Json.JObject json)
        {
            using var writer = new Newtonsoft.Json.JsonTextWriter(console.Out)
            {
                Formatting = Newtonsoft.Json.Formatting.Indented
            };

            WriteJson(writer, json);
            console.Out.WriteLine();

            static void WriteJson(Newtonsoft.Json.JsonTextWriter writer, Neo.IO.Json.JObject json)
            {
                switch (json)
                {
                    case null:
                        writer.WriteNull();
                        break;
                    case Neo.IO.Json.JBoolean boolean:
                        writer.WriteValue(boolean.Value);
                        break;
                    case Neo.IO.Json.JNumber number:
                        writer.WriteValue(number.Value);
                        break;
                    case Neo.IO.Json.JString @string:
                        writer.WriteValue(@string.Value);
                        break;
                    case Neo.IO.Json.JArray @array:
                        writer.WriteStartArray();
                        foreach (var value in @array)
                        {
                            WriteJson(writer, value);
                        }
                        writer.WriteEndArray();
                        break;
                    case Neo.IO.Json.JObject @object:
                        writer.WriteStartObject();
                        foreach (var (key, value) in @object.Properties)
                        {
                            writer.WritePropertyName(key);
                            WriteJson(writer, value);
                        }
                        writer.WriteEndObject();
                        break;
                }
            }
        }


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
        
        
        private static JObject[] MakeFindStatesParams(
            UInt256 rootHash,
            UInt160 scriptHash,
            ReadOnlySpan<byte> prefix,
            ReadOnlySpan<byte> from = default (ReadOnlySpan<byte>),
            int? count = null)
        {
            JObject[] jobjectArray = new JObject[count.HasValue ? 5 : 4];
            jobjectArray[0] = (JObject) rootHash.ToString();
            jobjectArray[1] = (JObject) scriptHash.ToString();
            jobjectArray[2] = (JObject) Convert.ToBase64String(prefix);
            jobjectArray[3] = (JObject) Convert.ToBase64String(from);
            if (count.HasValue)
                jobjectArray[4] = (JObject) (double) count.Value;
            return jobjectArray;
        }
        public static async Task<ExpressRpcFoundStates> ExpressFindStatesAsync(this RpcClient rpcClient, UInt256 rootHash, UInt160 contractScriptHash, ReadOnlyMemory<byte> prefix, ReadOnlyMemory<byte> from = default, int? pageSize = null)
        {
            ExpressRpcFoundStates? states = null;
            var start = from.ToArray();
            while (true)
            {
                var @params = MakeFindStatesParams(rootHash, contractScriptHash, prefix.Span, start, pageSize);
                var response = await rpcClient.RpcSendAsync("findstates", @params).ConfigureAwait(false);
                var foundStates = ExpressRpcFoundStates.FromJson(response);
                
                if (states is null)
                {
                    states = foundStates;
                } else
                {
                    states.Results = states.Results.Concat(foundStates.Results).ToArray();
                }
                
                if (!foundStates.Truncated || foundStates.Results.Length == 0)
                {
                    return states;
                }
                start = Convert.FromBase64String(foundStates.Results[foundStates.Results.Length - 1].key);
            }
        }
    }
}
