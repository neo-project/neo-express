using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.BlockchainToolkit.Models;
using Newtonsoft.Json;

namespace NeoExpress
{
    static class ConsoleExtensions
    {
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
            using var _ = writer.WritePropertyArray(wallet.Name);
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

            using var _ = writer.WriteObject();
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
            using var writer = new JsonTextWriter(console.Out)
            {
                Formatting = Formatting.Indented
            };

            writer.WriteJson(json);
            console.Out.WriteLine();
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
