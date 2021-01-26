using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Neo;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;
using NeoExpress.Models;
using Newtonsoft.Json;

using FileAccess = System.IO.FileAccess;
using FileMode = System.IO.FileMode;
using StreamWriter = System.IO.StreamWriter;
using TextWriter = System.IO.TextWriter;

namespace NeoExpress
{
    class BlockchainOperations : IBlockchainOperations
    {
        readonly IFileSystem fileSystem;

        public BlockchainOperations(IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public string CreateChain(int nodeCount, string output, bool force)
        {
            output = ResolveChainFileName(output);
            if (fileSystem.File.Exists(output))
            {
                if (force)
                {
                    fileSystem.File.Delete(output);
                }
                else
                {
                    throw new Exception("You must specify --force to overwrite an existing file");
                }
            }

            if (fileSystem.File.Exists(output))
            {
                throw new ArgumentException($"{output} already exists", nameof(output));
            }

            if (nodeCount != 1 && nodeCount != 4 && nodeCount != 7)
            {
                throw new ArgumentException("invalid blockchain node count", nameof(nodeCount));
            }

            var wallets = new List<(DevWallet wallet, Neo.Wallets.WalletAccount account)>(nodeCount);

            for (var i = 1; i <= nodeCount; i++)
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
            var nodes = new List<ExpressConsensusNode>(nodeCount);
            for (var i = 0; i < nodeCount; i++)
            {
                nodes.Add(new ExpressConsensusNode()
                {
                    TcpPort = GetPortNumber(i, 3),
                    WebSocketPort = GetPortNumber(i, 4),
                    RpcPort = GetPortNumber(i, 2),
                    Wallet = wallets[i].wallet.ToExpressWallet()
                });
            }

            var chain = new ExpressChain()
            {
                Magic = ExpressChain.GenerateMagicValue(),
                ConsensusNodes = nodes,
            };
            SaveChain(chain, output);
            return output;

            static ushort GetPortNumber(int index, ushort portNumber) => (ushort)(50000 + ((index + 1) * 10) + portNumber);
        }

        public IStore GetNodeStore(ExpressConsensusNode node, bool discard)
        {
            var folder = GetNodePath(node);

            if (discard)
            {
                try
                {
                    var rocksDbStore = RocksDbStore.OpenReadOnly(folder);
                    return new CheckpointStore(rocksDbStore);
                }
                catch
                {
                    return new CheckpointStore(NullReadOnlyStore.Instance);
                }
            }
            else
            {
                return RocksDbStore.Open(folder);
            }
        }

        public Func<IStore, ExpressConsensusNode, bool, TextWriter, CancellationToken, Task> GetNodeRunner(ExpressChain chain, uint secondsPerBlock)
        {
            chain.InitalizeProtocolSettings(secondsPerBlock);
            return RunAsync;
        }

        static async Task RunAsync(IStore store, ExpressConsensusNode node, bool enableTrace, TextWriter writer, CancellationToken token)
        {
            if (IsRunning(node))
            {
                throw new Exception("Node already running");
            }
            
            await writer.WriteLineAsync(store.GetType().Name).ConfigureAwait(false);

            var tcs = new TaskCompletionSource<bool>();
            _ = Task.Run(() =>
            {
                try
                {
                    var defaultAccount = node.Wallet.Accounts.Single(a => a.IsDefault);
                    using var mutex = new Mutex(true, GLOBAL_PREFIX + defaultAccount.ScriptHash);

                    var wallet = DevWallet.FromExpressWallet(node.Wallet);
                    var multiSigAccount = node.Wallet.Accounts.Single(a => a.IsMultiSigContract());

                    var logPlugin = new Node.LogPlugin(writer);
                    var storageProvider = new Node.ExpressStorageProvider(store);
                    var appEngineProvider = enableTrace ? new Node.ExpressApplicationEngineProvider() : null;
                    var appLogsPlugin = new Node.ExpressAppLogsPlugin(store);

                    using var system = new Neo.NeoSystem(storageProvider.Name);
                    var rpcSettings = new Neo.Plugins.RpcServerSettings(port: node.RpcPort);
                    var rpcServer = new Neo.Plugins.RpcServer(system, rpcSettings);
                    var expressRpcServer = new Node.ExpressRpcServer(multiSigAccount);
                    rpcServer.RegisterMethods(expressRpcServer);
                    rpcServer.RegisterMethods(appLogsPlugin);
                    rpcServer.StartRpcServer();

                    system.StartNode(new Neo.Network.P2P.ChannelsConfig
                    {
                        Tcp = new IPEndPoint(IPAddress.Loopback, node.TcpPort),
                        WebSocket = new IPEndPoint(IPAddress.Loopback, node.WebSocketPort),
                    });
                    system.StartConsensus(wallet);

                    token.WaitHandle.WaitOne();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
                finally
                {
                    tcs.TrySetResult(true);
                }
            });

            await tcs.Task.ConfigureAwait(false);
        }

        const string GLOBAL_PREFIX = "Global\\";

        static bool IsRunning(ExpressConsensusNode node)
        {
            // Check to see if there's a neo-express blockchain currently running by
            // attempting to open a mutex with the multisig account address for a name

            var account = node.Wallet.Accounts.Single(a => a.IsDefault);
            return Mutex.TryOpenExisting(GLOBAL_PREFIX + account.ScriptHash, out var _);
        }

        public string GetNodePath(ExpressConsensusNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (node.Wallet == null) throw new ArgumentNullException(nameof(node.Wallet));
            var account = node.Wallet.Accounts.Single(a => a.IsDefault);

            var rootPath = fileSystem.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify),
                "Neo-Express", 
                "blockchain-nodes");
            return fileSystem.Path.Combine(rootPath, account.ScriptHash);
        }

        public void ResetNode(ExpressConsensusNode node, bool force)
        {
            if (IsRunning(node))
            {
                var scriptHash = node.Wallet.DefaultAccount?.ScriptHash ?? "<unknown>";
                throw new InvalidOperationException($"node {scriptHash} currently running");
            }

            var folder = GetNodePath(node);
            if (fileSystem.Directory.Exists(folder))
            {
                if (!force)
                {
                    throw new InvalidOperationException("--force must be specified when resetting a node");
                }

                fileSystem.Directory.Delete(folder, true);
            }
        }

        public IExpressNode GetExpressNode(ExpressChain chain, bool offlineTrace = false)
        {
            // Check to see if there's a neo-express blockchain currently running by
            // attempting to open a mutex with the multisig account address for a name

            foreach (var consensusNode in chain.ConsensusNodes)
            {
                if (IsRunning(consensusNode))
                {
                    chain.InitalizeProtocolSettings();
                    return new Node.OnlineNode(consensusNode);
                }
            }

            var node = chain.ConsensusNodes[0];
            var folder = GetNodePath(node);
            if (!fileSystem.Directory.Exists(folder)) fileSystem.Directory.CreateDirectory(folder);

            chain.InitalizeProtocolSettings();
            return new Node.OfflineNode(RocksDbStore.Open(folder), node.Wallet, chain, offlineTrace);
        }

        internal const string EXPRESS_EXTENSION = ".neo-express";
        internal const string DEFAULT_EXPRESS_FILENAME = "default" + EXPRESS_EXTENSION;

        string ResolveChainFileName(string filename) 
        {
            if (string.IsNullOrEmpty(filename))
            {
                return fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), DEFAULT_EXPRESS_FILENAME);
            }

            filename = fileSystem.Path.IsPathFullyQualified(filename)
                ? filename : fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), filename);

            return EXPRESS_EXTENSION.Equals(fileSystem.Path.GetExtension(filename), StringComparison.OrdinalIgnoreCase)
                ? filename : filename + EXPRESS_EXTENSION;
        }

        public (Models.ExpressChain chain, string filename) LoadChain(string filename)
        {
            filename = ResolveChainFileName(filename);
            if (!fileSystem.File.Exists(filename))
            {
                throw new Exception($"{filename} file doesn't exist");
            }
            var serializer = new JsonSerializer();
            using var stream = fileSystem.File.OpenRead(filename);
            using var reader = new JsonTextReader(new System.IO.StreamReader(stream));
            var chain = serializer.Deserialize<ExpressChain>(reader)
                ?? throw new Exception($"Cannot load Neo-Express instance information from {filename}");

            return (chain, filename);
        }


        public void SaveChain(ExpressChain chain, string fileName)
        {
            var serializer = new JsonSerializer();
            using (var stream = fileSystem.File.Open(fileName, System.IO.FileMode.Create, System.IO.FileAccess.Write))
            using (var writer = new JsonTextWriter(new System.IO.StreamWriter(stream)) { Formatting = Formatting.Indented })
            {
                serializer.Serialize(writer, chain);
            }
        }

        private const string GENESIS = "genesis";

        public ExpressWallet CreateWallet(ExpressChain chain, string name)
        {
            if (IsReservedName())
            {
                throw new Exception($"{name} is a reserved name. Choose a different wallet name.");
            }

            if (chain.Wallets.Any(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new Exception($"There is already a wallet named {name}. Choose a different wallet name.");
            }

            var wallet = new DevWallet(name);
            var account = wallet.CreateAccount();
            account.IsDefault = true;
            return wallet.ToExpressWallet();

            bool IsReservedName()
            {
                if (string.Equals(GENESIS, name, StringComparison.OrdinalIgnoreCase))
                    return true;

                foreach (var node in chain.ConsensusNodes)
                {
                    if (string.Equals(node.Wallet.Name, name, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                return false;
            }
        }

        public void ExportChain(ExpressChain chain, string password, TextWriter writer)
        {
            var folder = fileSystem.Directory.GetCurrentDirectory();
            for (var i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var node = chain.ConsensusNodes[i];
                writer.WriteLine($"Exporting {node.Wallet.Name} Conensus Node wallet");

                var walletPath = fileSystem.Path.Combine(folder, $"{node.Wallet.Name}.wallet.json");
                if (fileSystem.File.Exists(walletPath))
                {
                    fileSystem.File.Delete(walletPath);
                }

                var devWallet = DevWallet.FromExpressWallet(node.Wallet);
                devWallet.Export(walletPath, password);

                var path = fileSystem.Path.Combine(folder, $"{node.Wallet.Name}.config.json");
                using var stream = fileSystem.File.Open(path, FileMode.Create, FileAccess.Write);
                using var configWriter = new JsonTextWriter(new StreamWriter(stream)) { Formatting = Formatting.Indented };

                // use neo-cli defaults for Logger & Storage

                configWriter.WriteStartObject();
                configWriter.WritePropertyName("ApplicationConfiguration");
                configWriter.WriteStartObject();

                configWriter.WritePropertyName("P2P");
                configWriter.WriteStartObject();
                configWriter.WritePropertyName("Port");
                configWriter.WriteValue(node.TcpPort);
                configWriter.WritePropertyName("WsPort");
                configWriter.WriteValue(node.WebSocketPort);
                configWriter.WriteEndObject();

                configWriter.WritePropertyName("UnlockWallet");
                configWriter.WriteStartObject();
                configWriter.WritePropertyName("Path");
                configWriter.WriteValue(walletPath);
                configWriter.WritePropertyName("Password");
                configWriter.WriteValue(password);
                configWriter.WritePropertyName("StartConsensus");
                configWriter.WriteValue(true);
                configWriter.WritePropertyName("IsActive");
                configWriter.WriteValue(true);
                configWriter.WriteEndObject();

                configWriter.WriteEndObject();
                configWriter.WriteEndObject();
            }

            {
                var path = fileSystem.Path.Combine(folder, "protocol.json");
                using var stream = fileSystem.File.Open(path, FileMode.Create, FileAccess.Write);
                using var protocolWriter = new JsonTextWriter(new StreamWriter(stream)) { Formatting = Formatting.Indented };

                // use neo defaults for MillisecondsPerBlock & AddressVersion

                protocolWriter.WriteStartObject();
                protocolWriter.WritePropertyName("ProtocolConfiguration");
                protocolWriter.WriteStartObject();

                protocolWriter.WritePropertyName("Magic");
                protocolWriter.WriteValue(chain.Magic);
                protocolWriter.WritePropertyName("ValidatorsCount");
                protocolWriter.WriteValue(chain.ConsensusNodes.Count);

                protocolWriter.WritePropertyName("StandbyCommittee");
                protocolWriter.WriteStartArray();
                for (int i = 0; i < chain.ConsensusNodes.Count; i++)
                {
                    var expressAccount = chain.ConsensusNodes[i].Wallet.DefaultAccount ?? throw new Exception("Invalid DefaultAccount");
                    var devAccount = DevWalletAccount.FromExpressWalletAccount(expressAccount);
                    var key = devAccount.GetKey();
                    if (key != null)
                    {
                        protocolWriter.WriteValue(key.PublicKey.EncodePoint(true).ToHexString());
                    }
                }
                protocolWriter.WriteEndArray();

                protocolWriter.WritePropertyName("SeedList");
                protocolWriter.WriteStartArray();
                foreach (var node in chain.ConsensusNodes)
                {
                    protocolWriter.WriteValue($"{IPAddress.Loopback}:{node.TcpPort}");
                }
                protocolWriter.WriteEndArray();

                protocolWriter.WriteEndObject();
                protocolWriter.WriteEndObject();
            }
        }
    }
}
