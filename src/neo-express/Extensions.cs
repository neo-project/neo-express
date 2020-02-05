using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Neo;
using Neo.Wallets;
using NeoExpress.Models;
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

namespace NeoExpress
{
    using StringError = OneOf.Types.Error<string>;

    internal static class Extensions
    {
        public static JObject Sign(this ExpressWalletAccount account, byte[] data)
        {
            var (signature, publicKey) = BlockchainOperations.Sign(account, data);

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

        public static JArray Sign(this ExpressWalletAccount account, IEnumerable<ExpressConsensusNode> nodes, JToken? json)
        {
            if (json == null)
            {
                throw new ArgumentException(nameof(json));
            }

            var data = json.Value<string>("hash-data").ToByteArray();

            // TODO: better way to identify the genesis account?
            if (account.Label == "MultiSigContract")
            {
                var hashes = json["script-hashes"].Select(t => t.Value<string>());
                var signatures = nodes.SelectMany(n => n.Wallet.Sign(hashes, data));
                return new JArray(signatures);
            }
            else
            {
                return new JArray(account.Sign(data));
            }
        }

        public static string ToHexString(this byte[] value, bool reverse = false)
        {
            StringBuilder sb = new StringBuilder();

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

        public static void WriteResult(this IConsole console, JToken? result)
        {
            if (result != null)
            {
                console.WriteLine(result.ToString(Formatting.Indented));
            }
            else
            {
                console.WriteLine("<no result provided>");
            }
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
            if ("genesis".Equals(name, StringComparison.InvariantCultureIgnoreCase))
                return true;

            foreach (var node in chain.ConsensusNodes)
            {
                if (string.Equals(name, node.Wallet.Name, StringComparison.InvariantCultureIgnoreCase))
                    return true;
            }

            return false;
        }

        public static bool NameEquals(this ExpressContract contract, string name) =>
            string.Equals(contract.Name, name, StringComparison.InvariantCultureIgnoreCase);

        public static bool NameEquals(this ExpressWallet wallet, string name) =>
            string.Equals(wallet.Name, name, StringComparison.InvariantCultureIgnoreCase);

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
                    "ProtocolConfiguration:AddressVersion", $"{(byte)0x17}");
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

        private static bool TryGetContractFile(string path, [NotNullWhen(true)] out string? contractPath, [MaybeNullWhen(true)] out string errorMessage)
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

        public static ExpressContract GetContract(this ExpressChain chain, string nameOrPath)
        {
            static AbiContract LoadAbiContract(string avmFile)
            {
                string abiFile = Path.ChangeExtension(avmFile, ".abi.json");
                if (!File.Exists(abiFile))
                {
                    throw new ApplicationException($"there is no .abi.json file for {avmFile}.");
                }

                var serializer = new JsonSerializer();
                using var stream = File.OpenRead(abiFile);
                using var reader = new JsonTextReader(new StreamReader(stream));
                return serializer.Deserialize<AbiContract>(reader)
                    ?? throw new ApplicationException($"Cannot load contract abi information from {abiFile}");
            }

            static ExpressContract.Function ToExpressContractFunction(AbiContract.Function function)
                => new ExpressContract.Function
                {
                    Name = function.Name,
                    ReturnType = function.ReturnType,
                    Parameters = function.Parameters.Select(p => new ExpressContract.Parameter
                    {
                        Name = p.Name,
                        Type = p.Type
                    }).ToList()
                };

            static Dictionary<string, string> ToExpressContractProperties(AbiContract.ContractMetadata? metadata)
                => metadata == null
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string>()
                    {
                                { "title", metadata.Title },
                                { "description", metadata.Description },
                                { "version", metadata.Version },
                                { "email", metadata.Email },
                                { "author", metadata.Author },
                                { "has-storage", metadata.HasStorage.ToString() },
                                { "has-dynamic-invoke", metadata.HasDynamicInvoke.ToString() },
                                { "is-payable", metadata.IsPayable.ToString() }
                    };

            if (TryGetContractFile(nameOrPath, out var avmFile, out var errorMessage))
            {
                System.Diagnostics.Debug.Assert(File.Exists(avmFile));

                var abiContract = LoadAbiContract(avmFile);
                var name = Path.GetFileNameWithoutExtension(avmFile);
                return new ExpressContract()
                {
                    Name = name,
                    Hash = abiContract.Hash,
                    EntryPoint = abiContract.Entrypoint,
                    ContractData = File.ReadAllBytes(avmFile).ToHexString(),
                    Functions = abiContract.Functions.Select(ToExpressContractFunction).ToList(),
                    Events = abiContract.Events.Select(ToExpressContractFunction).ToList(),
                    Properties = ToExpressContractProperties(abiContract.Metadata),
                };
            }

            // if the file can't be found, see if the path is 
            // actually the name of an existing contract
            foreach (var contract in chain.Contracts ?? Enumerable.Empty<ExpressContract>())
            {
                if (contract.NameEquals(nameOrPath))
                {
                    return contract;
                }
            }

            throw new ApplicationException(errorMessage);
        }

        public static void SaveContract(this ExpressChain chain, ExpressContract contract, string filename, IConsole console, bool overwrite)
        {
            for (var i = chain.Contracts.Count - 1; i >= 0; i--)
            {
                var c = chain.Contracts[i];
                if (string.Equals(contract.Hash, c.Hash))
                {
                    chain.Contracts.RemoveAt(i);
                }
                else if (string.Equals(contract.Name, c.Name, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (overwrite)
                    {
                        console.WriteWarning($"Overriting contract named {c.Name} that already exists with a different hash value.");
                        chain.Contracts.RemoveAt(i);
                    }
                    else
                    {
                        console.WriteWarning($"Contract named {c.Name} already exists with a different hash value.");
                        if (Prompt.GetYesNo("Overwrite?", false))
                        {
                            chain.Contracts.RemoveAt(i);
                        }
                        else
                        {
                            console.WriteWarning($"{Path.GetFileName(filename)} not updated with new {c.Name} contract info.");
                            return;
                        }
                    }
                }
            }

            chain.Contracts.Add(contract);
            chain.Save(filename);
            console.WriteLine($"Contract {contract.Name} info saved to {Path.GetFileName(filename)}");
        }
    }
}
