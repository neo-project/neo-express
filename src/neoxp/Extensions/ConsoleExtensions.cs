using System;
using System.IO;
using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit.Models;
using Neo.Network.RPC.Models;
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

        public static void WriteResult(this TextWriter @this, RpcInvokeResult result, bool json)
        {
            if (json)
            {
                using var writer = new JsonTextWriter(@this) { Formatting = Formatting.Indented };
                writer.WriteJson(result.ToJson());
            }
            else
            {
                @this.WriteLine($"VM State:     {result.State}");
                @this.WriteLine($"Gas Consumed: {result.GasConsumed}");
                if (!string.IsNullOrEmpty(result.Exception))
                {
                    @this.WriteLine($"Exception:   {result.Exception}");
                }
                if (result.Stack.Length > 0)
                {
                    var stack = result.Stack;
                    @this.WriteLine("Result Stack:");
                    for (int i = 0; i < stack.Length; i++)
                    {
                        @this.WriteStackItem(stack[i]);
                    }
                }
            }
        }

        public static void WriteStackItem(this TextWriter writer, Neo.VM.Types.StackItem item, int indent = 1, string prefix = "")
        {
            switch (item)
            {
                case Neo.VM.Types.Boolean _:
                    WriteLine(item.GetBoolean() ? "true" : "false");
                    break;
                case Neo.VM.Types.Integer @int:
                    WriteLine($"{@int.GetInteger()}");
                    break;
                case Neo.VM.Types.Buffer buffer:
                    WriteLine(Convert.ToHexString(buffer.GetSpan()));
                    break;
                case Neo.VM.Types.ByteString byteString:
                    WriteLine(Convert.ToHexString(byteString.GetSpan()));
                    break;
                case Neo.VM.Types.Null _:
                    WriteLine($"<null>");
                    break;
                case Neo.VM.Types.Array array:
                    WriteLine($"{(array is Neo.VM.Types.Struct ? "Struct" : "Array")}: ({array.Count})");
                    for (int i = 0; i < array.Count; i++)
                    {
                        WriteStackItem(writer, array[i], indent + 1);
                    }
                    break;
                case Neo.VM.Types.Map map:
                    WriteLine($"Array: ({map.Count})");
                    foreach (var m in map)
                    {
                        WriteStackItem(writer, m.Key, indent + 1, "key:   ");
                        WriteStackItem(writer, m.Value, indent + 1, "value: ");
                    }
                    break;
            }

            void WriteLine(string value)
            {
                for (var i = 0; i < indent; i++)
                {
                    writer.Write("  ");
                }

                if (!string.IsNullOrEmpty(prefix))
                {
                    writer.Write(prefix);
                }

                writer.WriteLine(value);
            }
        }

    }
}