using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.Wallets;
using NeoExpress.Models;
using NeoExpress.Node;
using Nito.Disposables;

namespace NeoExpress
{
    internal class ExpressChainManager : IExpressChainManager
    {
        const string GLOBAL_PREFIX = "Global\\";
        const string CHECKPOINT_EXTENSION = ".neoxp-checkpoint";

        readonly IFileSystem fileSystem;
        readonly ExpressChain chain;
        public ProtocolSettings ProtocolSettings { get; }

        public ExpressChainManager(IFileSystem fileSystem, ExpressChain chain, uint secondsPerBlock)
        {
            this.fileSystem = fileSystem;
            this.chain = chain;
            this.ProtocolSettings = chain.GetProtocolSettings(secondsPerBlock);
        }

        public ExpressChain Chain => chain;

        string ResolveCheckpointFileName(string path) => fileSystem.ResolveFileName(path, CHECKPOINT_EXTENSION, () => $"{DateTimeOffset.Now:yyyyMMdd-hhmmss}");

        static bool IsRunning(ExpressConsensusNode node)
        {
            // Check to see if there's a neo-express blockchain currently running by
            // attempting to open a mutex with the multisig account address for a name

            var account = node.Wallet.Accounts.Single(a => a.IsDefault);
            return Mutex.TryOpenExisting(GLOBAL_PREFIX + account.ScriptHash, out var _);
        }

        public async Task<(string path, bool online)> CreateCheckpointAsync(IExpressNode expressNode, string checkPointPath, bool force)
        {
            if (chain.ConsensusNodes.Count != 1)
            {
                throw new ArgumentException("Checkpoint create is only supported on single node express instances", nameof(chain));
            }

            checkPointPath = ResolveCheckpointFileName(checkPointPath);
            if (fileSystem.File.Exists(checkPointPath))
            {
                if (force)
                {
                    fileSystem.File.Delete(checkPointPath);
                }
                else
                {
                    throw new Exception("You must specify --force to overwrite an existing file");
                }
            }

            var parentPath = fileSystem.Path.GetDirectoryName(checkPointPath);
            if (!fileSystem.Directory.Exists(parentPath))
            {
                fileSystem.Directory.CreateDirectory(parentPath);
            }

            var online = await expressNode.CreateCheckpointAsync(checkPointPath).ConfigureAwait(false);
            return (checkPointPath, online);
        }

        public void RestoreCheckpoint(string checkPointArchive, bool force)
        {
            if (chain.ConsensusNodes.Count != 1)
            {
                throw new ArgumentException("Checkpoint restore is only supported on single node express instances", nameof(chain));
            }

            checkPointArchive = ResolveCheckpointFileName(checkPointArchive);
            if (!fileSystem.File.Exists(checkPointArchive))
            {
                throw new Exception($"Checkpoint {checkPointArchive} couldn't be found");
            }

            var node = chain.ConsensusNodes[0];
            if (IsRunning(node))
            {
                var scriptHash = node.Wallet.DefaultAccount?.ScriptHash ?? "<unknown>";
                throw new InvalidOperationException($"node {scriptHash} currently running");
            }

            var checkpointTempPath = fileSystem.GetTempFolder();
            using var folderCleanup = AnonymousDisposable.Create(() =>
            {
                if (fileSystem.Directory.Exists(checkpointTempPath))
                {
                    fileSystem.Directory.Delete(checkpointTempPath, true);
                }
            });

            var nodeFolder = fileSystem.GetNodePath(node);
            if (fileSystem.Directory.Exists(nodeFolder))
            {
                if (force)
                {
                    fileSystem.Directory.Delete(nodeFolder, true);
                }
                else
                {
                    throw new Exception("You must specify force to restore a checkpoint to an existing blockchain.");
                }
            }

            var wallet = DevWallet.FromExpressWallet(ProtocolSettings, node.Wallet);
            var multiSigAccount = wallet.GetMultiSigAccounts().Single();
            RocksDbStorageProvider.RestoreCheckpoint(checkPointArchive, checkpointTempPath, ProtocolSettings, multiSigAccount.ScriptHash);
            fileSystem.Directory.Move(checkpointTempPath, nodeFolder);
        }

        public void SaveChain(string path)
        {
            fileSystem.SaveChain(chain, path);
        }

        public void ResetNode(ExpressConsensusNode node, bool force)
        {
            if (IsRunning(node))
            {
                var scriptHash = node.Wallet.DefaultAccount?.ScriptHash ?? "<unknown>";
                throw new InvalidOperationException($"node {scriptHash} currently running");
            }

            var folder = fileSystem.GetNodePath(node);
            if (fileSystem.Directory.Exists(folder))
            {
                if (!force)
                {
                    throw new InvalidOperationException("--force must be specified when resetting a node");
                }

                fileSystem.Directory.Delete(folder, true);
            }
        }

        public async Task RunAsync(IStorageProvider store, ExpressConsensusNode node, bool enableTrace, TextWriter writer, CancellationToken token)
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

                    var wallet = DevWallet.FromExpressWallet(ProtocolSettings, node.Wallet);
                    var multiSigAccount = wallet.GetMultiSigAccounts().Single();

                    var logPlugin = new Node.LogPlugin(writer);
                    var storageProviderPlugin = new Node.StorageProviderPlugin(store);
                    var appEngineProvider = enableTrace ? new Node.ExpressApplicationEngineProvider() : null;
                    var dbftPlugin = new Neo.Consensus.DBFTPlugin(GetConsensusSettings(chain));
                    var appLogsPlugin = new Node.ExpressAppLogsPlugin(store);

                    using var neoSystem = new Neo.NeoSystem(ProtocolSettings, storageProviderPlugin.Name);
                    var rpcSettings = GetRpcServerSettings(chain, node);
                    var rpcServer = new Neo.Plugins.RpcServer(neoSystem, rpcSettings);
                    var expressRpcServer = new ExpressRpcServer(neoSystem, store, multiSigAccount.ScriptHash);
                    rpcServer.RegisterMethods(expressRpcServer);
                    rpcServer.RegisterMethods(appLogsPlugin);
                    rpcServer.StartRpcServer();

                    neoSystem.StartNode(new Neo.Network.P2P.ChannelsConfig
                    {
                        Tcp = new IPEndPoint(IPAddress.Loopback, node.TcpPort),
                        WebSocket = new IPEndPoint(IPAddress.Loopback, node.WebSocketPort),
                    });
                    dbftPlugin.Start(wallet);

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

            static Neo.Consensus.Settings GetConsensusSettings(ExpressChain chain)
            {
                var settings = new Dictionary<string, string>()
                {
                    { "PluginConfiguration:Network", $"{chain.Magic}" }
                };

                var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
                return new Neo.Consensus.Settings(config.GetSection("PluginConfiguration"));
            }

            static RpcServerSettings GetRpcServerSettings(ExpressChain chain, ExpressConsensusNode node)
            {
                var settings = new Dictionary<string, string>()
                {
                    { "PluginConfiguration:Network", $"{chain.Magic}" },
                    { "PluginConfiguration:BindAddress", $"{IPAddress.Loopback}" },
                    { "PluginConfiguration:Port", $"{node.RpcPort}" }
                };

                if (chain.TryReadSetting<long>("rpc.MaxGasInvoke", long.TryParse, out var maxGasInvoke))
                {
                    settings.Add("PluginConfiguration:MaxGasInvoke", $"{maxGasInvoke}");
                }

                if (chain.TryReadSetting<long>("rpc.MaxFee", long.TryParse, out var maxFee))
                {
                    settings.Add("PluginConfiguration:MaxFee", $"{maxFee}");
                }

                var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
                return RpcServerSettings.Load(config.GetSection("PluginConfiguration"));
            }
        }

        public IDisposableStorageProvider GetNodeStorageProvider(ExpressConsensusNode node, bool discard)
        {
            var folder = fileSystem.GetNodePath(node);
            if (!fileSystem.Directory.Exists(folder)) fileSystem.Directory.CreateDirectory(folder);

            if (discard)
            {
                try
                {
                    var rocksDbStore = RocksDbStorageProvider.OpenReadOnly(folder);
                    return new CheckpointStorageProvider(rocksDbStore);
                }
                catch
                {
                    return new CheckpointStorageProvider(null);
                }
            }
            else
            {
                return RocksDbStorageProvider.Open(folder);
            }
        }

        public IDisposableStorageProvider GetCheckpointStorageProvider(string checkPointPath)
        {
            if (chain.ConsensusNodes.Count != 1)
            {
                throw new ArgumentException("Checkpoint restore is only supported on single node express instances", nameof(chain));
            }

            var node = chain.ConsensusNodes[0];
            if (IsRunning(node)) throw new Exception($"node already running");

            checkPointPath = ResolveCheckpointFileName(checkPointPath);
            if (!fileSystem.File.Exists(checkPointPath))
            {
                throw new Exception($"Checkpoint {checkPointPath} couldn't be found");
            }

            var checkpointTempPath = fileSystem.GetTempFolder();
            var folderCleanup = AnonymousDisposable.Create(() =>
            {
                if (fileSystem.Directory.Exists(checkpointTempPath))
                {
                    fileSystem.Directory.Delete(checkpointTempPath, true);
                }
            });

            var wallet = DevWallet.FromExpressWallet(ProtocolSettings, node.Wallet);
            var multiSigAccount = wallet.GetMultiSigAccounts().Single();
            RocksDbStorageProvider.RestoreCheckpoint(checkPointPath, checkpointTempPath, chain.Magic, chain.AddressVersion, multiSigAccount.ScriptHash);
            var rocksDbStorageProvider = RocksDbStorageProvider.OpenReadOnly(checkpointTempPath);
            return new CheckpointStorageProvider(rocksDbStorageProvider, checkpointCleanup: folderCleanup);
        }

        public IExpressNode GetExpressNode(bool offlineTrace = false)
        {
            // Check to see if there's a neo-express blockchain currently running by
            // attempting to open a mutex with the multisig account address for a name

            for (int i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var consensusNode = chain.ConsensusNodes[i];
                if (IsRunning(consensusNode))
                {
                    return new Node.OnlineNode(ProtocolSettings, chain, consensusNode);
                }
            }

            var node = chain.ConsensusNodes[0];
            var nodePath = fileSystem.GetNodePath(node);
            if (!fileSystem.Directory.Exists(nodePath)) fileSystem.Directory.CreateDirectory(nodePath);

            return new Node.OfflineNode(ProtocolSettings,
                RocksDbStorageProvider.Open(nodePath),
                node.Wallet,
                chain,
                offlineTrace);
        }
    }
}
