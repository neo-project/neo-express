using System;
using System.IO;
using Neo.BlockchainToolkit.Models;
using Newtonsoft.Json;

namespace NeoExpress
{
    static class ConsoleExtensions
    {
        public static void WriteWallet(this TextWriter writer, ExpressWallet wallet)
        {
            writer.WriteLine(wallet.Name);

            foreach (var account in wallet.Accounts)
            {
                WriteAccount(writer, account);
            }

            static void WriteAccount(TextWriter writer, ExpressWalletAccount account)
            {
                var keyPair = new Neo.Wallets.KeyPair(Convert.FromHexString(account.PrivateKey));
                var address = account.ScriptHash;
                var scriptHash = Neo.IO.Helper.ToArray(account.GetScriptHash());

                writer.WriteLine($"  {address} ({(account.IsDefault ? "Default" : account.Label)})");
                writer.WriteLine($"    script hash: {BitConverter.ToString(scriptHash)}");
                writer.WriteLine($"    public key:  {Convert.ToHexString(keyPair.PublicKey.EncodePoint(true))}");
                writer.WriteLine($"    private key: {Convert.ToHexString(keyPair.PrivateKey)}");
            }
        }

        public static void WriteWallet(this JsonTextWriter writer, ExpressWallet wallet)
        {
            using var _ = writer.WritePropertyArray(wallet.Name);
            foreach (var account in wallet.Accounts)
            {
                WriteAccount(writer, wallet.Name, account);
            }

            static void WriteAccount(JsonTextWriter writer, string walletName, ExpressWalletAccount account)
            {
                var keyPair = new Neo.Wallets.KeyPair(Convert.FromHexString(account.PrivateKey));
                var address = account.ScriptHash;
                var scriptHash = account.GetScriptHash();

                using var _ = writer.WriteObject();
                writer.WriteProperty("wallet-name", walletName);
                writer.WriteProperty("account-label", account.Label ?? (account.IsDefault ? "Default" : string.Empty));
                writer.WriteProperty("address", address);
                writer.WriteProperty("script-hash", scriptHash.ToString());
                writer.WriteProperty("private-key", Convert.ToHexString(keyPair.PrivateKey));
                writer.WriteProperty("public-key", Convert.ToHexString(keyPair.PublicKey.EncodePoint(true)));
            }
        }
    }
}