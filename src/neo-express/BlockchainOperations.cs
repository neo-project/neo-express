using Neo;
using Neo.SmartContract;
using NeoExpress.Models;
using NeoExpress.Node;
using NeoExpress.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace NeoExpress
{
    internal static class BlockchainOperations
    {
        public static ExpressChain CreateBlockchain(int count)
        {
            var wallets = new List<(DevWallet wallet, Neo.Wallets.WalletAccount account)>(count);

            ushort GetPortNumber(int index, ushort portNumber) => (ushort)((49000 + (index * 1000)) + portNumber);

            try
            {
                for (var i = 1; i <= count; i++)
                {
                    var wallet = new DevWallet($"node{i}");
                    var account = wallet.CreateAccount();
                    account.IsDefault = true;
                    wallets.Add((wallet, account));
                }

                var keys = wallets.Select(t => t.account.GetKey().PublicKey).ToArray();

                var contract = Neo.SmartContract.Contract.CreateMultiSigContract((keys.Length * 2 / 3) + 1, keys);

                foreach (var (wallet, account) in wallets)
                {
                    var multiSigContractAccount = wallet.CreateAccount(contract, account.GetKey());
                    multiSigContractAccount.Label = "MultiSigContract";
                }

                // 49152 is the first port in the "Dynamic and/or Private" range as specified by IANA
                // http://www.iana.org/assignments/port-numbers
                var nodes = new List<ExpressConsensusNode>(count);
                for (var i = 0; i < count; i++)
                {
                    nodes.Add(new ExpressConsensusNode()
                    {
                        TcpPort = GetPortNumber(i, 333),
                        WebSocketPort = GetPortNumber(i, 334),
                        RpcPort = GetPortNumber(i, 332),
                        Wallet = wallets[i].wallet.ToExpressWallet()
                    });
                }

                return new ExpressChain()
                {
                    Magic = ExpressChain.GenerateMagicValue(),
                    ConsensusNodes = nodes,
                };
            }
            finally
            {
                foreach (var (wallet, _) in wallets)
                {
                    wallet.Dispose();
                }
            }
        }

        public static void ExportBlockchain(ExpressChain chain, string folder, string password, Action<string> writeConsole)
        {
            void WriteNodeConfigJson(ExpressConsensusNode _node, string walletPath)
            {
                using (var stream = File.Open(Path.Combine(folder, $"{_node.Wallet.Name}.config.json"), FileMode.Create, FileAccess.Write))
                using (var writer = new JsonTextWriter(new StreamWriter(stream)) { Formatting = Formatting.Indented })
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("ApplicationConfiguration");
                    writer.WriteStartObject();

                    writer.WritePropertyName("Paths");
                    writer.WriteStartObject();
                    writer.WritePropertyName("Chain");
                    writer.WriteValue("Chain_{0}");
                    writer.WritePropertyName("Index");
                    writer.WriteValue("Index_{0}");
                    writer.WriteEndObject();

                    writer.WritePropertyName("P2P");
                    writer.WriteStartObject();
                    writer.WritePropertyName("Port");
                    writer.WriteValue(_node.TcpPort);
                    writer.WritePropertyName("WsPort");
                    writer.WriteValue(_node.WebSocketPort);
                    writer.WriteEndObject();

                    writer.WritePropertyName("RPC");
                    writer.WriteStartObject();
                    writer.WritePropertyName("BindAddress");
                    writer.WriteValue("127.0.0.1");
                    writer.WritePropertyName("Port");
                    writer.WriteValue(_node.RpcPort);
                    writer.WritePropertyName("SslCert");
                    writer.WriteValue("");
                    writer.WritePropertyName("SslCertPassword");
                    writer.WriteValue("");
                    writer.WriteEndObject();

                    writer.WritePropertyName("UnlockWallet");
                    writer.WriteStartObject();
                    writer.WritePropertyName("Path");
                    writer.WriteValue(walletPath);
                    writer.WritePropertyName("Password");
                    writer.WriteValue(password);
                    writer.WritePropertyName("StartConsensus");
                    writer.WriteValue(true);
                    writer.WritePropertyName("IsActive");
                    writer.WriteValue(true);
                    writer.WriteEndObject();

                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }
            }

            void WriteProtocolJson()
            {
                using (var stream = File.Open(Path.Combine(folder, "protocol.json"), FileMode.Create, FileAccess.Write))
                using (var writer = new JsonTextWriter(new StreamWriter(stream)) { Formatting = Formatting.Indented })
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("ProtocolConfiguration");
                    writer.WriteStartObject();

                    writer.WritePropertyName("Magic");
                    writer.WriteValue(chain.Magic);
                    writer.WritePropertyName("AddressVersion");
                    writer.WriteValue(23);
                    writer.WritePropertyName("SecondsPerBlock");
                    writer.WriteValue(15);

                    writer.WritePropertyName("StandbyValidators");
                    writer.WriteStartArray();
                    for (int i = 0; i < chain.ConsensusNodes.Count; i++)
                    {
                        var account = DevWalletAccount.FromExpressWalletAccount(chain.ConsensusNodes[i].Wallet.DefaultAccount);
                        var key = account.GetKey();
                        if (key != null)
                        {
                            writer.WriteValue(key.PublicKey.EncodePoint(true).ToHexString());
                        }
                    }
                    writer.WriteEndArray();

                    writer.WritePropertyName("SeedList");
                    writer.WriteStartArray();
                    foreach (var node in chain.ConsensusNodes)
                    {
                        writer.WriteValue($"{IPAddress.Loopback}:{node.TcpPort}");
                    }
                    writer.WriteEndArray();

                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }
            }

            for (var i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var node = chain.ConsensusNodes[i];
                writeConsole($"Exporting {node.Wallet.Name} Conensus Node wallet");

                var walletPath = Path.Combine(folder, $"{node.Wallet.Name}.wallet.json");
                if (File.Exists(walletPath))
                {
                    File.Delete(walletPath);
                }

                ExportWallet(node.Wallet, walletPath, password);
                WriteNodeConfigJson(node, walletPath);
            }

            WriteProtocolJson();
        }

        public static ExpressWallet CreateWallet(string name)
        {
            using (var wallet = new DevWallet(name))
            {
                var account = wallet.CreateAccount();
                account.IsDefault = true;
                return wallet.ToExpressWallet();
            }
        }

        public static void ExportWallet(ExpressWallet wallet, string filename, string password)
        {
            var devWallet = DevWallet.FromExpressWallet(wallet);
            devWallet.Export(filename, password);
        }

        public static (byte[] signature, byte[] publicKey) Sign(ExpressWalletAccount account, byte[] data)
        {
            var devAccount = DevWalletAccount.FromExpressWalletAccount(account);

            var key = devAccount.GetKey();
            if (key == null)
                throw new InvalidOperationException();

            var publicKey = key.PublicKey.EncodePoint(false).AsSpan().Slice(1).ToArray();
            var signature = Neo.Cryptography.Crypto.Default.Sign(data, key.PrivateKey, publicKey);
            return (signature, key.PublicKey.EncodePoint(true));
        }

        private const string ADDRESS_FILENAME = "ADDRESS.neo-express";

        private static string GetAddressFilePath(string directory) =>
            Path.Combine(directory, ADDRESS_FILENAME);

        public static async Task<string> CreateCheckpoint(ExpressChain chain, string checkPointFileName)
        {
            static bool NodeRunning(ExpressConsensusNode node)
            {
                // Check to see if there's a neo-express blockchain currently running
                // by attempting to open a mutex with the multisig account address for 
                // a name. If so, do an online checkpoint instead of offline.

                if (Mutex.TryOpenExisting(node.GetMultiSigAddress(), out var _))
                {
                    return true;
                }

                return false;
            }

            if (File.Exists(checkPointFileName))
            {
                throw new ArgumentException("Checkpoint file already exists", nameof(checkPointFileName));
            }

            if (chain.ConsensusNodes.Count != 1)
            {
                throw new ArgumentException("Checkpoint create is only supported on single node express instances", nameof(chain));
            }

            var node = chain.ConsensusNodes[0];
            var folder = node.GetBlockchainPath();

            if (NodeRunning(node))
            {
                var uri = chain.GetUri();
                await NeoRpcClient.ExpressCreateCheckpoint(uri, checkPointFileName)
                    .ConfigureAwait(false);
                return $"Created {Path.GetFileName(checkPointFileName)} checkpoint online";
            }
            else
            {
                using var db = new RocksDbStore(folder);
                CreateCheckpoint(db, checkPointFileName, chain.Magic, chain.ConsensusNodes[0].Wallet.DefaultAccount.ScriptHash);
                return $"Created {Path.GetFileName(checkPointFileName)} checkpoint offline";
            }
        }

        public static void CreateCheckpoint(RocksDbStore db, string checkPointFileName, long magic, string scriptHash)
        {
            string tempPath;
            do
            {
                tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            }
            while (Directory.Exists(tempPath));

            try
            {
                db.CheckPoint(tempPath);

                using (var stream = File.OpenWrite(GetAddressFilePath(tempPath)))
                using (var writer = new StreamWriter(stream))
                {
                    writer.WriteLine(magic);
                    writer.WriteLine(scriptHash);
                }

                if (File.Exists(checkPointFileName))
                {
                    throw new InvalidOperationException(checkPointFileName + " checkpoint file already exists");
                }
                System.IO.Compression.ZipFile.CreateFromDirectory(tempPath, checkPointFileName);
            }
            finally
            {
                Directory.Delete(tempPath, true);
            }
        }

        public static void RestoreCheckpoint(ExpressChain chain, string chainDirectory, string checkPointDirectory)
        {
            var node = chain.ConsensusNodes[0];
            ValidateCheckpoint(checkPointDirectory, chain.Magic, node.Wallet.DefaultAccount);

            var addressFile = GetAddressFilePath(checkPointDirectory);
            if (!File.Exists(addressFile))
            {
                File.Delete(addressFile);
            }

            Directory.Move(checkPointDirectory, chainDirectory);
        }

        private static void ValidateCheckpoint(string checkPointDirectory, long magic, ExpressWalletAccount account)
        {
            var addressFile = GetAddressFilePath(checkPointDirectory);
            if (!File.Exists(addressFile))
            {
                throw new Exception("Invalid Checkpoint");
            }

            long checkPointMagic;
            string scriptHash;
            using (var stream = File.OpenRead(addressFile))
            using (var reader = new StreamReader(stream))
            {
                checkPointMagic = long.Parse(reader.ReadLine() ?? string.Empty);
                scriptHash = reader.ReadLine() ?? string.Empty;
            }

            if (magic != checkPointMagic || scriptHash != account.ScriptHash)
            {
                throw new Exception("Invalid Checkpoint");
            }
        }

        public static void PreloadGas(string directory, ExpressChain chain, int index, uint preloadGasAmount, TextWriter writer, CancellationToken cancellationToken)
        {
            if (!chain.InitializeProtocolSettings())
            {
                throw new Exception("could not initialize protocol settings");
            }
            var node = chain.ConsensusNodes[index];
            using var store = new RocksDbStore(directory);
            NodeUtility.Preload(preloadGasAmount, store , node, writer, cancellationToken);
        }

        public static async Task RunBlockchainAsync(string directory, ExpressChain chain, int index, uint secondsPerBlock, TextWriter writer, CancellationToken cancellationToken)
        {
            if (!chain.InitializeProtocolSettings(secondsPerBlock))
            {
                throw new Exception("could not initialize protocol settings");
            }

            // create a named mutex so that checkpoint create command
            // can detect if blockchain is running automatically
            var node = chain.ConsensusNodes[index];
            using var mutex = new Mutex(true, node.GetMultiSigAddress());

            writer.WriteLine(directory);

            // NodeUtility.RunAsync disposes the store when it's done
            await NodeUtility.RunAsync(new RocksDbStore(directory), node, writer, cancellationToken);
        }

        public static async Task RunCheckpointAsync(string directory, ExpressChain chain, uint secondsPerBlock, TextWriter writer, CancellationToken cancellationToken)
        {
            if (!chain.InitializeProtocolSettings(secondsPerBlock))
            {
                throw new Exception("could not initialize protocol settings");
            }
            var node = chain.ConsensusNodes[0];
            ValidateCheckpoint(directory, chain.Magic, node.Wallet.DefaultAccount);

            // NodeUtility.RunAsync disposes the store when it's done
            await NodeUtility.RunAsync(new CheckpointStore(directory), node, writer, cancellationToken);
        }

        static Task<T?> SwallowException<T>(Task<T?> task)
            where T : class
        {
            return task.ContinueWith(t =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    return task.Result;
                }
                else
                {
                    return null;
                }
            });
        }

        public static async Task<Neo.Network.P2P.Payloads.InvocationTransaction> DeployContract(ExpressChain chain, ExpressContract contract, ExpressWalletAccount account, bool saveMetadata = true)
        {
            var uri = chain.GetUri();

            var contractState = await SwallowException(NeoRpcClient.GetContractState(uri, contract.Hash))
                .ConfigureAwait(false);

            if (contractState != null)
            {
                throw new Exception($"Contract {contract.Name} ({contract.Hash}) already deployed");
            }

            var unspents = (await NeoRpcClient.GetUnspents(uri, account.ScriptHash)
                .ConfigureAwait(false))?.ToObject<UnspentsResponse>();
            if (unspents == null)
            {
                throw new Exception($"could not retrieve unspents for account");
            }

            var tx = RpcTransactionManager.CreateDeploymentTransaction(contract,
                account, unspents);
            tx.Witnesses = new[] { RpcTransactionManager.GetWitness(tx, chain, account) };

            var sendResult = await NeoRpcClient.SendRawTransaction(uri, tx);
            if (sendResult == null || !sendResult.Value<bool>())
            {
                throw new Exception("SendRawTransaction failed");
            }

            if (saveMetadata)
            {
                var abiContract = ConvertContract(contract);
                await NeoRpcClient.SaveContractMetadata(uri, abiContract.Hash, abiContract);
            }

            return tx;
        }

        static ExpressContract ConvertContract(AbiContract abiContract, string? name = null, string? contractData = null)
        {
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

            var properties = abiContract.Metadata == null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>()
                {
                    { "title", abiContract.Metadata.Title },
                    { "description", abiContract.Metadata.Description },
                    { "version", abiContract.Metadata.Version },
                    { "email", abiContract.Metadata.Email },
                    { "author", abiContract.Metadata.Author },
                    { "has-storage", abiContract.Metadata.HasStorage.ToString() },
                    { "has-dynamic-invoke", abiContract.Metadata.HasDynamicInvoke.ToString() },
                    { "is-payable", abiContract.Metadata.IsPayable.ToString() }
                };

            return new ExpressContract()
            {
                Name = name ?? abiContract.Metadata?.Title ?? string.Empty,
                Hash = abiContract.Hash,
                EntryPoint = abiContract.Entrypoint,
                ContractData = contractData ?? string.Empty,
                Functions = abiContract.Functions.Select(ToExpressContractFunction).ToList(),
                Events = abiContract.Events.Select(ToExpressContractFunction).ToList(),
                Properties = properties
            };
        }

        static ExpressContract ConvertContract(ContractState contractState)
        {
            var properties = new Dictionary<string, string>()
            {
                { "has-dynamic-invoke", contractState.Properties.DynamicInvoke.ToString() },
                { "has-storage", contractState.Properties.Storage.ToString() }
            };

            var entrypoint = "Main";
            var @params = contractState.Parameters.Select((type, index) =>
                new ExpressContract.Parameter()
                {
                    Name = $"parameter{index}",
                    Type = type
                }
            );

            var function = new ExpressContract.Function()
            {
                Name = entrypoint,
                Parameters = @params.ToList(),
                ReturnType = contractState.ReturnType,
            };

            return new ExpressContract()
            {
                Name = contractState.Name,
                Hash = contractState.Hash,
                EntryPoint = entrypoint,
                ContractData = contractState.Script,
                Functions = new List<ExpressContract.Function>() { function },
                Properties = properties
            };

        }

        static AbiContract ConvertContract(ExpressContract contract)
        {
            static AbiContract.Function ToAbiContractFunction(ExpressContract.Function function)
                => new AbiContract.Function
                {
                    Name = function.Name,
                    ReturnType = function.ReturnType,
                    Parameters = function.Parameters.Select(p => new AbiContract.Parameter
                    {
                        Name = p.Name,
                        Type = p.Type
                    }).ToList()
                };

            static AbiContract.ContractMetadata ToAbiContractMetadata(Dictionary<string, string> metadata)
            {
                var contractMetadata = new AbiContract.ContractMetadata();
                if (metadata.TryGetValue("title", out var title))
                {
                    contractMetadata.Title = title;
                }
                if (metadata.TryGetValue("description", out var description))
                {
                    contractMetadata.Description = description;
                }
                if (metadata.TryGetValue("version", out var version))
                {
                    contractMetadata.Version = version;
                }
                if (metadata.TryGetValue("email", out var email))
                {
                    contractMetadata.Description = email;
                }
                if (metadata.TryGetValue("author", out var author))
                {
                    contractMetadata.Author = author;
                }
                if (metadata.TryGetValue("has-storage", out var hasStorageString)
                    && bool.TryParse(hasStorageString, out var hasStorage))
                {
                    contractMetadata.HasStorage = hasStorage;
                }
                if (metadata.TryGetValue("has-dynamic-invoke", out var hasDynamicInvokeString)
                    && bool.TryParse(hasDynamicInvokeString, out var hasDynamicInvoke))
                {
                    contractMetadata.HasDynamicInvoke = hasDynamicInvoke;
                }
                if (metadata.TryGetValue("is-payable", out var isPayableString)
                    && bool.TryParse(hasStorageString, out var isPayable))
                {
                    contractMetadata.IsPayable = isPayable;
                }

                return contractMetadata;
            }

            return new AbiContract()
            {
                Hash = contract.Hash,
                Entrypoint = contract.EntryPoint,
                Functions = contract.Functions.Select(ToAbiContractFunction).ToList(),
                Events = contract.Events.Select(ToAbiContractFunction).ToList(),
                Metadata = ToAbiContractMetadata(contract.Properties)
            };
        }

        public static bool TryLoadContract(string path, [MaybeNullWhen(false)] out ExpressContract contract, [MaybeNullWhen(true)] out string errorMessage)
        {
            if (Directory.Exists(path))
            {
                var avmFiles = Directory.EnumerateFiles(path, "*.avm");
                var avmFileCount = avmFiles.Count();
                if (avmFileCount == 1)
                {
                    contract = LoadContract(avmFiles.Single());
                    errorMessage = null!;
                    return true;
                }

                contract = null!;
                errorMessage = avmFileCount == 0
                    ? $"There are no .avm files in {path}"
                    : $"There is more than one .avm file in {path}. Please specify file name directly";
                return false;
            }

            if (File.Exists(path) && Path.GetExtension(path) == ".avm")
            {
                contract = LoadContract(path);
                errorMessage = null!;
                return true;
            }

            contract = null!;
            errorMessage = $"{path} is not an .avm file.";
            return false;
        }

        static ExpressContract LoadContract(string avmFile)
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

            System.Diagnostics.Debug.Assert(File.Exists(avmFile));

            var abiContract = LoadAbiContract(avmFile);
            var name = Path.GetFileNameWithoutExtension(avmFile);
            var contractData = File.ReadAllBytes(avmFile).ToHexString();
            return ConvertContract(abiContract, name, contractData);
        }

        public static async Task<ExpressContract?> GetContract(ExpressChain chain, string scriptHash)
        {
            var uri = chain.GetUri();

            var getContractStateTask = SwallowException(NeoRpcClient.GetContractState(uri, scriptHash));
            var getContractMetadataTask = SwallowException(NeoRpcClient.GetContractMetadata(uri, scriptHash));
            await Task.WhenAll(getContractStateTask, getContractMetadataTask);

            if (getContractStateTask.Result != null
                && getContractMetadataTask.Result != null)
            {
                var contractData = getContractMetadataTask.Result.Value<string>("script");
                var name = getContractMetadataTask.Result.Value<string>("name");
                var abiContract = getContractMetadataTask.Result.ToObject<AbiContract>();

                if (abiContract != null)
                {
                    return ConvertContract(abiContract, name, contractData);
                }
            }

            if (getContractStateTask.Result != null)
            {
                var contractState = getContractStateTask.Result.ToObject<ContractState>();
                if (contractState != null)
                {
                    return ConvertContract(contractState);
                }
            }

            throw new Exception($"Contract {scriptHash} not deployed");
        }

        public static async Task<List<ExpressContract>> ListContracts(ExpressChain chain)
        {
            var uri = chain.GetUri();
            var json = await NeoRpcClient.ListContracts(uri);

            if (json != null && json is JArray jObject)
            {
                var contracts = new List<ExpressContract>(jObject.Count);
                foreach (var obj in jObject)
                {
                    var type = obj.Value<string>("type");
                    if (type == "metadata")
                    {
                        var contract = obj.ToObject<AbiContract>();
                        Debug.Assert(contract != null);
                        contracts.Add(ConvertContract(contract!));
                    }
                    else
                    {
                        Debug.Assert(type == "state");
                        var contract = obj.ToObject<ContractState>();
                        Debug.Assert(contract != null);
                        contracts.Add(ConvertContract(contract!));
                    }
                }

                return contracts;
            }

            return new List<ExpressContract>(0);
        }

        public static async Task<List<ExpressStorage>> GetStorage(ExpressChain chain, string scriptHash)
        {
            var uri = chain.GetUri();
            var json = await NeoRpcClient.ExpressGetContractStorage(uri, scriptHash);
            if (json != null && json is JArray array)
            {
                var storages = new List<ExpressStorage>(array.Count);
                foreach (var s in array)
                {
                    var storage = new ExpressStorage()
                    {
                        Key = s.Value<string>("key"),
                        Value = s.Value<string>("value"),
                        Constant = s.Value<bool>("constant")
                    };
                    storages.Add(storage);
                }
                return storages;
            }

            return new List<ExpressStorage>(0);
        }

        public static async Task<Neo.Network.P2P.Payloads.InvocationTransaction> InvokeContract(ExpressChain chain, string invocationFilePath, ExpressWalletAccount account)
        {
            var uri = chain.GetUri();
            var (scriptHash, args) = await LoadInvocationFileScript(invocationFilePath).ConfigureAwait(false);
            var script = RpcTransactionManager.CreateInvocationScript(scriptHash, args);
            var invokeResponse = (await NeoRpcClient.InvokeScript(uri, script))?.ToObject<InvokeResponse>();
            var gasConsumed = invokeResponse == null ? 0 : invokeResponse.GasConsumed;

            var tx = RpcTransactionManager.CreateInvocationTransaction(account, script, gasConsumed);
            tx.Witnesses = new[] { RpcTransactionManager.GetWitness(tx, chain, account) };

            var sendResult = await NeoRpcClient.SendRawTransaction(uri, tx);
            if (sendResult == null || !sendResult.Value<bool>())
            {
                throw new Exception("SendRawTransaction failed");
            }

            return tx;
        }

        static ContractParameter ParseContractParamString(string value)
        {
            if (value.StartsWith("@A"))
            {
                try
                {
                    var paramValue = Neo.Wallets.Helper.ToScriptHash(value.Substring(1));
                    return new ContractParameter
                    {
                        Type = ContractParameterType.Hash160,
                        Value = paramValue,
                    };
                }
                catch (FormatException) {} // ignore format exceptions
            }

            if (value.StartsWith("0x"))
            {
                try
                {
                    var paramValue = value.Substring(2).HexToBytes(); 
                    return new ContractParameter
                    {
                        Type = ContractParameterType.ByteArray,
                        Value = paramValue,
                    };
                }
                catch (FormatException) {} // ignore format exceptions
            }

            return new ContractParameter
            {
                Type = ContractParameterType.String,
                Value = value
            };
        }

        static ContractParameter ParseContractParamObject(JObject json)
        {
            // This logic mirrors ContractParameter.FromJson, except that it uses 
            // ParseContractParam for converting array/map elements. It also 
            // uses the Newtonsoft.Json library instead of Neo's JSON library

            var type = Enum.Parse<ContractParameterType>(json.Value<string>("type"));
            object value = type switch
            {
                ContractParameterType.ByteArray => json.Value<string>("value").HexToBytes(),
                ContractParameterType.Signature => json.Value<string>("value").HexToBytes(),
                ContractParameterType.Boolean => json.Value<bool>("value"),
                ContractParameterType.Integer => BigInteger.Parse(json.Value<string>("value")),
                ContractParameterType.Hash160 => UInt160.Parse(json.Value<string>("value")),
                ContractParameterType.Hash256 => UInt256.Parse(json.Value<string>("value")),
                ContractParameterType.PublicKey => Neo.Cryptography.ECC.ECPoint.Parse(json.Value<string>("value"), Neo.Cryptography.ECC.ECCurve.Secp256r1),
                ContractParameterType.String => json.Value<string>("value"),
                ContractParameterType.Array => json["value"].Select(ParseContractParam).ToList(),
                ContractParameterType.Map => json["value"].Select(ParseMapElement).ToList(),
                _ => throw new ArgumentException(nameof(json)),
            };

            return new ContractParameter
            {
                Type = type,
                Value = value
            };

            static KeyValuePair<ContractParameter, ContractParameter> ParseMapElement(JToken json)
            {
                var key = ParseContractParam(json["key"] ?? throw new ArgumentException(nameof(json)));
                var value = ParseContractParam(json["value"] ?? throw new ArgumentException(nameof(json)));
                return KeyValuePair.Create(key, value);
            }
        }

        static ContractParameter ParseContractParam(JToken json) => json.Type switch
        {
            JTokenType.Boolean => new ContractParameter
            {
                Type = ContractParameterType.Boolean,
                Value = json.Value<bool>()
            },
            JTokenType.Integer => new ContractParameter
            {
                Type = ContractParameterType.Integer,
                Value = new BigInteger(json.Value<int>())
            },
            JTokenType.Array => new ContractParameter
            {
                Type = ContractParameterType.Array,
                Value = json.Select(ParseContractParam).ToList()
            },
            JTokenType.String => ParseContractParamString(json.Value<string>()),
            JTokenType.Object => ParseContractParamObject((JObject)json),
            _ => throw new ArgumentException(nameof(json))
        };

        static async Task<(UInt160 scriptHash, IReadOnlyList<ContractParameter> args)> LoadInvocationFileScript(string invocationFilePath)
        {
            static async Task<JObject> LoadInvocationFileJson(string path)
            {
                using var fileStream = File.OpenRead(path);
                using var reader = new StreamReader(fileStream);
                using var jreader = new JsonTextReader(reader);
                return await JObject.LoadAsync(jreader);
            }
            
            var json = await LoadInvocationFileJson(invocationFilePath).ConfigureAwait(false);

            var scriptHash = UInt160.Parse(json.Value<string>("hash"));
            var args = (json["args"] ?? Enumerable.Empty<JToken>()).Select(ParseContractParam).ToList();
            return (scriptHash, args);
        }


        public static async Task<InvokeResponse> TestInvokeContract(ExpressChain chain, string invocationFilePath)
        {
            var uri = chain.GetUri();
            var (scriptHash, args) = await LoadInvocationFileScript(invocationFilePath).ConfigureAwait(false);
            var script = RpcTransactionManager.CreateInvocationScript(scriptHash, args);
            var response = await NeoRpcClient.InvokeScript(uri, script).ConfigureAwait(false);
            return response?.ToObject<InvokeResponse>() ?? throw new Exception("invalid response");
        }
    }
}
