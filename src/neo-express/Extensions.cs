using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NeoExpress
{
    internal static class Extensions
    {
        public static JObject Sign(this ExpressWalletAccount account, byte[] data, INeoBackend backend = null)
        {
            var (signature, publicKey) = (backend ?? Program.GetBackend()).Sign(account, data);

            return new JObject
            {
                ["signature"] = signature.ToHexString(),
                ["public-key"] = publicKey.ToHexString(),
                ["contract"] = new JObject
                {
                    ["script"] = account.Contract.Script,
                    ["parameters"] = new JArray(account.Contract.Parameters)
                }
            };
        }

        public static IEnumerable<JObject> Sign(this ExpressWallet wallet, IEnumerable<string> hashes, byte[] data, INeoBackend backend = null)
        {
            backend = backend ?? Program.GetBackend();
            foreach (var hash in hashes)
            {
                var account = wallet.Accounts.SingleOrDefault(a => a.ScriptHash == hash);
                if (account == null || string.IsNullOrEmpty(account.PrivateKey))
                    continue;

                yield return account.Sign(data, backend);
            }
        }

        public static string ToHexString(this byte[] value)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                sb.AppendFormat("{0:x2}", value[i]);
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

        public static bool NameEquals(this ExpressWallet wallet, string name) =>
            wallet.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase);
        
        public static ExpressWallet GetWallet(this ExpressChain chain, string name) => 
            (chain.Wallets ?? Enumerable.Empty<ExpressWallet>())
                .SingleOrDefault(w => w.NameEquals(name));

        public static string GetBlockchainPath(this ExpressWalletAccount account)
        {
            if (account == null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            return Path.Combine(Program.ROOT_PATH, account.ScriptHash);
        }

        public static ExpressContract GetContract(this ExpressChain chain, string name)
        {
            if (chain.Contracts != null)
            {
                return chain.Contracts.SingleOrDefault(c => c.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
            }

            return default;
        }

        public static ExpressWalletAccount GetAccount(this ExpressChain chain, string name)
        {
            var wallet = chain.Wallets.SingleOrDefault(w => w.NameEquals(name));
            if (wallet != default)
            {
                return wallet.DefaultAccount;
            }

            var node = chain.ConsensusNodes.SingleOrDefault(n => n.Wallet.NameEquals(name));
            if (node != default)
            {
                return node.Wallet.DefaultAccount;
            }

            if (name.Equals("genesis", StringComparison.InvariantCultureIgnoreCase))
            {
                return chain.ConsensusNodes
                    .Select(n => n.Wallet.Accounts.Single(a => a.Label == "MultiSigContract"))
                    .SingleOrDefault();
            }

            return default;
        }

        public static Uri GetUri(this ExpressChain chain, int node = 0) => new Uri($"http://localhost:{chain.ConsensusNodes[node].RpcPort}");

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
