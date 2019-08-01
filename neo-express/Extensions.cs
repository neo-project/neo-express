using McMaster.Extensions.CommandLineUtils;
using Neo.SmartContract;
using Neo.Wallets;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Neo.Express
{
    internal static class Extensions
    {
        public static JObject Sign(this WalletAccount account, byte[] data)
        {
            var key = account.GetKey();
            //var publicKey = key.PublicKey.EncodePoint(false).Skip(1).ToArray();
            var publicKey = key.PublicKey.EncodePoint(false).AsSpan().Slice(1).ToArray();
            var signature = Cryptography.Crypto.Default.Sign(data, key.PrivateKey, publicKey);

            return new JObject
            {
                ["signature"] = signature.ToHexString(),
                ["public-key"] = key.PublicKey.EncodePoint(true).ToHexString(),
                ["contract"] = new JObject
                {
                    ["script"] = account.Contract.Script.ToHexString(),
                    ["parameters"] = new JArray(account.Contract.ParameterList.Select(cpt => Enum.GetName(typeof(ContractParameterType), cpt)))
                }
            };
        }

        public static IEnumerable<JObject> Sign(this DevWallet wallet, IEnumerable<UInt160> hashes, byte[] data)
        {
            foreach (var hash in hashes)
            {
                var account = wallet.GetAccount(hash);
                if (account == null || !account.HasKey)
                    continue;

                yield return Sign(account, data);
            }
        }

        static void WriteMessage(IConsole console, string message, ConsoleColor color)
        {
            var currentColor = console.ForegroundColor;
            try
            {
                console.ForegroundColor = color;
                console.WriteLine(message);
            }
            finally
            {
                console.ForegroundColor = currentColor;
            }
        }

        public static void WriteError(this IConsole console, string message)
        {
            WriteMessage(console, message, ConsoleColor.Red);
        }

        public static void WriteWarning(this IConsole console, string message)
        {
            WriteMessage(console, message, ConsoleColor.Yellow);
        }
    }
}
