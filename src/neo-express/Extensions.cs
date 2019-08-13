using McMaster.Extensions.CommandLineUtils;
using Neo.Express.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.Express
{
    internal static class Extensions
    {
        //public static JObject Sign(this WalletAccount account, byte[] data)
        //{
        //    var key = account.GetKey();
        //    //var publicKey = key.PublicKey.EncodePoint(false).Skip(1).ToArray();
        //    var publicKey = key.PublicKey.EncodePoint(false).AsSpan().Slice(1).ToArray();
        //    var signature = Cryptography.Crypto.Default.Sign(data, key.PrivateKey, publicKey);

        //    return new JObject
        //    {
        //        ["signature"] = signature.ToHexString(),
        //        ["public-key"] = key.PublicKey.EncodePoint(true).ToHexString(),
        //        ["contract"] = new JObject
        //        {
        //            ["script"] = account.Contract.Script.ToHexString(),
        //            ["parameters"] = new JArray(account.Contract.ParameterList.Select(cpt => Enum.GetName(typeof(ContractParameterType), cpt)))
        //        }
        //    };
        //}

        //public static IEnumerable<JObject> Sign(this DevWallet wallet, IEnumerable<UInt160> hashes, byte[] data)
        //{
        //    foreach (var hash in hashes)
        //    {
        //        var account = wallet.GetAccount(hash);
        //        if (account == null || !account.HasKey)
        //            continue;

        //        yield return Sign(account, data);
        //    }
        //}

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

        public static void Save(this ExpressChain chain, string fileName)
        {
            var serializer = new JsonSerializer();
            using (var stream = File.Open(fileName, FileMode.Create, FileAccess.Write))
            using (var writer = new JsonTextWriter(new StreamWriter(stream)) { Formatting = Formatting.Indented })
            {
                serializer.Serialize(writer, chain);
            }
        }

        public static bool IsReservedName(this ExpressChain chain, string name)
        {
            if (name.Equals("genesis", StringComparison.InvariantCultureIgnoreCase))
                return true;

            foreach (var node in chain.ConsensusNodes)
            {
                if (name.Equals(node.Wallet.Name, StringComparison.InvariantCultureIgnoreCase))
                    return true;
            }

            return false;
        }

        public static ExpressWallet GetWallet(this ExpressChain chain, string name) => 
            (chain.Wallets ?? Enumerable.Empty<ExpressWallet>())
                .SingleOrDefault(w => name.Equals(w.Name, StringComparison.InvariantCultureIgnoreCase));

        public static string GetBlockchainPath(this ExpressWalletAccount account)
        {
            if (account == null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            return Path.Combine(Program.ROOT_PATH, account.ScriptHash);
        }

        public static string GetBlockchainPath(this ExpressWallet wallet)
        {
            if (wallet == null)
            {
                throw new ArgumentNullException(nameof(wallet));
            }

            return wallet.Accounts
                .Single(a => a.IsDefault)
                .GetBlockchainPath();
        }

        public static string GetBlockchainPath(this ExpressConsensusNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            return node.Wallet.GetBlockchainPath();
        }
    }
}
