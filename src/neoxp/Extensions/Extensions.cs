// Copyright (C) 2015-2023 The Neo Project.
//
// Extensions.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.BlockchainToolkit;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.Wallets;
using NeoExpress.Models;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

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

        public static void WriteJson(this IConsole console, Neo.Json.JObject json)
        {
            using var writer = new Newtonsoft.Json.JsonTextWriter(console.Out)
            {
                Formatting = Newtonsoft.Json.Formatting.Indented
            };

            WriteJson(writer, json);
            console.Out.WriteLine();
        }

        public static void WriteWarning(string value)
        {
            Console.WriteLine($"\x1b[33mWarning: {value}\x1b[0m");
        }

        public static void WriteJson(this Newtonsoft.Json.JsonWriter writer, Neo.Json.JToken? json)
        {
            switch (json)
            {
                case null:
                    writer.WriteNull();
                    break;
                case Neo.Json.JBoolean boolean:
                    writer.WriteValue(boolean.Value);
                    break;
                case Neo.Json.JNumber number:
                    writer.WriteValue(new BigInteger(number.Value));
                    break;
                case Neo.Json.JString @string:
                    writer.WriteValue(@string.Value);
                    break;
                case Neo.Json.JArray @array:
                    writer.WriteStartArray();
                    foreach (var value in @array)
                    {
                        WriteJson(writer, value);
                    }
                    writer.WriteEndArray();
                    break;
                case Neo.Json.JObject @object:
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

        public static void WriteException(this CommandLineApplication app, Exception exception, bool showInnerExceptions = false)
        {
            var showStackTrace = ((CommandOption<bool>)app.GetOptions().Single(o => o.LongName == "stack-trace")).ParsedValue;

            app.Error.WriteLine($"\x1b[1m\x1b[31m\x1b[40m{exception.GetType()}: {exception.Message}\x1b[0m");

            if (showStackTrace)
                app.Error.WriteLine($"\x1b[1m\x1b[37m\x1b[40m{exception.StackTrace}\x1b[0m");

            if (showInnerExceptions || showStackTrace)
            {
                while (exception.InnerException is not null)
                {
                    app.Error.WriteLine($"\x1b[1m\x1b[33m\x1b[40m\tInner {exception.InnerException.GetType().Name}: {exception.InnerException.Message}\x1b[0m");
                    exception = exception.InnerException;
                }
            }
        }

        public static bool IsMultiSigContract(this WalletAccount @this) => Neo.SmartContract.Helper.IsMultiSigContract(@this.Contract.Script);

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
                if (!string.IsNullOrEmpty(txType))
                    await writer.WriteAsync($"{txType} ").ConfigureAwait(false);
                await writer.WriteLineAsync($"Transaction {txHash} submitted").ConfigureAwait(false);
            }
        }

        public static BigDecimal ToBigDecimal(this RpcNep17Balance balance, byte decimals)
            => new BigDecimal(balance.Amount, decimals);

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

                if (chainManager.Chain.Wallets is not null && chainManager.Chain.Wallets.Count > 0)
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
                        accountHash = wallet.GetAccounts().Single(a => !a.IsMultiSigContract()).ScriptHash;
                        return true;
                    }
                }

                if (TryGetWIFWallet(name, settings, out wallet, out accountHash))
                {
                    return true;
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

            static bool TryGetWIFWallet(string wif, ProtocolSettings settings, [MaybeNullWhen(false)] out Wallet wallet, [MaybeNullWhen(false)] out UInt160 accountHash)
            {
                try
                {
                    var privateKey = Wallet.GetPrivateKeyFromWIF(wif);
                    CreateWallet(privateKey, settings, out wallet, out accountHash);
                    return true;
                }
                catch
                {
                    wallet = null;
                    accountHash = null;
                    return false;
                }
            }

            static bool TryGetNEP2Wallet(string nep2, string password, ProtocolSettings settings, [MaybeNullWhen(false)] out Wallet wallet, [MaybeNullWhen(false)] out UInt160 accountHash)
            {
                try
                {
                    var privateKey = Wallet.GetPrivateKeyFromNEP2(nep2, password, settings.AddressVersion);
                    CreateWallet(privateKey, settings, out wallet, out accountHash);
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
                    var nep6wallet = new Neo.Wallets.NEP6.NEP6Wallet(path, password, settings);
                    var nep6account = nep6wallet.GetAccounts().SingleOrDefault(a => a.IsDefault)
                        ?? nep6wallet.GetAccounts().SingleOrDefault()
                        ?? throw new InvalidOperationException("Neo-express only supports NEP-6 wallets with a single default account or a single account");
                    if (nep6account.IsMultiSigContract())
                        throw new Exception("Neo-express doesn't supports multi-sig NEP-6 accounts");
                    var keyPair = nep6account.GetKey() ?? throw new Exception("account.GetKey() returned null");
                    CreateWallet(keyPair.PrivateKey, settings, out wallet, out accountHash);
                    return true;
                }
                catch
                {
                    wallet = null;
                    accountHash = null;
                    return false;
                }
            }

            static void CreateWallet(byte[] privateKey, ProtocolSettings settings, out Wallet wallet, out UInt160 accountHash)
            {
                wallet = new DevWallet(settings, string.Empty);
                var account = wallet.CreateAccount(privateKey);
                accountHash = account.ScriptHash;
            }


        }
    }
}
