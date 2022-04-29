using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Neo;
using Neo.BlockchainToolkit.Models;
using Newtonsoft.Json;
using Nito.Disposables;

namespace NeoExpress
{
    static class CommandLineAppExtensions
    {
        public static IFileSystem GetFileSystem(this CommandLineApplication app)
        {
            return ((IServiceProvider)app).GetRequiredService<IFileSystem>();
        }

        public static IExpressFile GetExpressFile(this CommandLineApplication app)
        {
            var option = app.GetOptions().Single(o => o.LongName == "input");
            var input = option.Value() ?? string.Empty;
            var fileSystem = app.GetFileSystem();
            return new ExpressFile(input, fileSystem);
        }
    }

    static class ConsoleExtensions
    {
        public static IDisposable WriteStartArrayAuto(this JsonWriter writer)
        {
            writer.WriteStartArray();
            return AnonymousDisposable.Create(() => writer.WriteEndArray());
        }

        public static IDisposable WriteStartObjectAuto(this JsonWriter writer)
        {
            writer.WriteStartObject();
            return AnonymousDisposable.Create(() => writer.WriteEndObject());
        }

        public static void WriteWallet(this TextWriter writer, ExpressWallet wallet)
        {
            writer.WriteLine(wallet.Name);

            foreach (var account in wallet.Accounts)
            {
                writer.WriteAccount(account);
            }
        }

        public static void WriteAccount(this TextWriter writer, ExpressWalletAccount account)
        {
            var keyPair = new Neo.Wallets.KeyPair(Convert.FromHexString(account.PrivateKey));
            var address = account.ScriptHash;
            var scriptHash = Neo.IO.Helper.ToArray(account.GetScriptHash());

            writer.WriteLine($"  {address} ({(account.IsDefault ? "Default" : account.Label)})");
            writer.WriteLine($"    script hash: {BitConverter.ToString(scriptHash)}");
            writer.WriteLine($"    public key:  {Convert.ToHexString(keyPair.PublicKey.EncodePoint(true))}");
            writer.WriteLine($"    private key: {Convert.ToHexString(keyPair.PrivateKey)}");
        }

        public static void WriteWallet(this JsonTextWriter writer, ExpressWallet wallet)
        {
            writer.WritePropertyName(wallet.Name);

            using var _ = writer.WriteStartArrayAuto();
            foreach (var account in wallet.Accounts)
            {
                writer.WriteAccount(account, wallet.Name);
            }
        }

        public static void WriteAccount(this JsonTextWriter writer, ExpressWalletAccount account, string walletName)
        {
            var keyPair = new Neo.Wallets.KeyPair(Convert.FromHexString(account.PrivateKey));
            var address = account.ScriptHash;
            var scriptHash = account.GetScriptHash();

            using var _ = writer.WriteStartObjectAuto();
            writer.WritePropertyName("wallet-name");
            writer.WriteValue(walletName);
            writer.WritePropertyName("account-label");
            writer.WriteValue(account.IsDefault ? "Default" : account.Label);
            writer.WritePropertyName("address");
            writer.WriteValue(address);
            writer.WritePropertyName("script-hash");
            writer.WriteValue(scriptHash.ToString());
            writer.WritePropertyName("private-key");
            writer.WriteValue(Convert.ToHexString(keyPair.PrivateKey));
            writer.WritePropertyName("public-key");
            writer.WriteValue(Convert.ToHexString(keyPair.PublicKey.EncodePoint(true)));
        }

        public static void WriteJson(this IConsole console, Neo.IO.Json.JObject json)
        {
            using var writer = new Newtonsoft.Json.JsonTextWriter(console.Out)
            {
                Formatting = Newtonsoft.Json.Formatting.Indented
            };

            WriteJson(writer, json);
            console.Out.WriteLine();
        }

        public static void WriteJson(this Newtonsoft.Json.JsonWriter writer, Neo.IO.Json.JObject json)
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
                    {
                        using var _ = writer.WriteStartArrayAuto();
                        foreach (var value in @array)
                        {
                            WriteJson(writer, value);
                        }
                        break;
                    }
                case Neo.IO.Json.JObject @object:
                    {
                        using var _ = writer.WriteStartObjectAuto();
                        foreach (var (key, value) in @object.Properties)
                        {
                            writer.WritePropertyName(key);
                            WriteJson(writer, value);
                        }
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
    }
}
