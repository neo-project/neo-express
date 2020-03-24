using Microsoft.Extensions.Configuration;
using Neo;
using Neo.Wallets;
using NeoExpress.Abstractions.Models;
using NeoExpress.Neo2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OneOf;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace NeoExpress.Neo2
{
    using StringError = OneOf.Types.Error<string>;

    internal static class Extensions
    {
        public static JObject Sign(this ExpressWalletAccount account, byte[] data)
        {
            var blockchainOperations = new BlockchainOperations();
            var (signature, publicKey) = blockchainOperations.Sign(account, data);

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

        public static IEnumerable<JObject> Sign(this ExpressWallet wallet, IEnumerable<string> hashes, byte[] data)
        {
            foreach (var hash in hashes)
            {
                var account = wallet.Accounts.SingleOrDefault(a => a.ScriptHash == hash);
                if (account == null || string.IsNullOrEmpty(account.PrivateKey))
                    continue;

                yield return account.Sign(data);
            }
        }

        public static string ToHexString(this byte[] value, bool reverse = false)
        {
            var sb = new StringBuilder();

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

        public static void WriteResult(this TextWriter writer, JToken? result)
        {
            if (result != null)
            {
                writer.WriteLine(result.ToString(Formatting.Indented));
            }
            else
            {
                writer.WriteLine("<no result provided>");
            }
        }

        public static bool NameEquals(this ExpressContract contract, string name) =>
            string.Equals(contract.Name, name, StringComparison.InvariantCultureIgnoreCase);

        public static bool NameEquals(this ExpressWallet wallet, string name) =>
            string.Equals(wallet.Name, name, StringComparison.InvariantCultureIgnoreCase);

       public static string ROOT_PATH => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Neo-Express", "blockchain-nodes");

        public static string GetBlockchainPath(this ExpressWalletAccount account)
        {
            if (account == null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            return Path.Combine(ROOT_PATH, account.ScriptHash);
        }

        public static ExpressWalletAccount? GetAccount(this ExpressChain chain, string name)
        {
            if (chain.Wallets != null)
            {
                var wallet = chain.Wallets.SingleOrDefault(w => w.NameEquals(name));
                if (wallet != null)
                {
                    return wallet.DefaultAccount;
                }
            }

            var node = chain.ConsensusNodes.SingleOrDefault(n => n.Wallet.NameEquals(name));
            if (node != null)
            {
                return node.Wallet.DefaultAccount;
            }

            if ("genesis".Equals(name, StringComparison.InvariantCultureIgnoreCase))
            {
                return chain.ConsensusNodes
                    .Select(n => n.Wallet.Accounts.Single(a => a.Label == "MultiSigContract"))
                    .FirstOrDefault();
            }

            return null;
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

        public static bool InitializeProtocolSettings(this ExpressChain chain, uint secondsPerBlock = 0)
        {
            secondsPerBlock = secondsPerBlock == 0 ? 15 : secondsPerBlock;

            IEnumerable<KeyValuePair<string, string>> settings()
            {
                yield return new KeyValuePair<string, string>(
                    "ProtocolConfiguration:Magic", $"{chain.Magic}");
                yield return new KeyValuePair<string, string>(
                    "ProtocolConfiguration:AddressVersion", $"{ExpressChain.AddressVersion}");
                yield return new KeyValuePair<string, string>(
                    "ProtocolConfiguration:SecondsPerBlock", $"{secondsPerBlock}");

                foreach (var (node, index) in chain.ConsensusNodes.Select((n, i) => (n, i)))
                {
                    var privateKey = node.Wallet.Accounts
                        .Select(a => a.PrivateKey)
                        .Distinct().Single().HexToBytes();
                    var encodedPublicKey = new KeyPair(privateKey).PublicKey
                        .EncodePoint(true).ToHexString();
                    yield return new KeyValuePair<string, string>(
                        $"ProtocolConfiguration:StandbyValidators:{index}", encodedPublicKey);
                    yield return new KeyValuePair<string, string>(
                        $"ProtocolConfiguration:SeedList:{index}", $"{IPAddress.Loopback}:{node.TcpPort}");
                }
            }

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings())
                .Build();

            return ProtocolSettings.Initialize(config);
        }

        public static bool TryGetContractFile(string path, [NotNullWhen(true)] out string? contractPath, [MaybeNullWhen(true)] out string errorMessage)
        {
            if (Directory.Exists(path))
            {
                var avmFiles = Directory.EnumerateFiles(path, "*.avm");
                var avmFileCount = avmFiles.Count();
                if (avmFileCount == 1)
                {
                    contractPath = avmFiles.Single();
                    errorMessage = null!;
                    return true;
                }

                contractPath = null;
                errorMessage = avmFileCount == 0
                    ? $"There are no .avm files in {path}"
                    : $"There is more than one .avm file in {path}. Please specify file name directly";
                return false;
            }

            if (File.Exists(path) && Path.GetExtension(path) == ".avm")
            {
                contractPath = path;
                errorMessage = null!;
                return true;
            }

            contractPath = null;
            errorMessage = $"{path} is not an .avm file.";
            return false;
        }
    }
}
