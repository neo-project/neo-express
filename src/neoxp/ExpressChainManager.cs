using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
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
        readonly Lazy<ProtocolSettings> protocolSettings;

        public ExpressChainManager(IFileSystem fileSystem, ExpressChain chain, uint secondsPerBlock)
        {
            this.fileSystem = fileSystem;
            this.chain = chain;
            this.protocolSettings = new Lazy<ProtocolSettings>(chain.GetProtocolSettings(secondsPerBlock));
        }

        public ExpressChain Chain => chain;
        public ProtocolSettings ProtocolSettings => protocolSettings.Value;

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
            RocksDbStore.RestoreCheckpoint(checkPointArchive, checkpointTempPath, ProtocolSettings, multiSigAccount.ScriptHash);
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

        public async Task RunAsync(IExpressStore store, ExpressConsensusNode node, uint secondsPerBlock, bool enableTrace, TextWriter writer, CancellationToken token)
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
                    var storageProvider = new Node.ExpressStorageProvider(store);
                    var appEngineProvider = enableTrace ? new Node.ExpressApplicationEngineProvider() : null;
                    // TODO: configure DBFTPlugin correctly once https://github.com/neo-project/neo-modules/pull/545 is merged
                    var dbftPlugin = new Neo.Consensus.DBFTPlugin();
                    var appLogsPlugin = new Node.ExpressAppLogsPlugin(store);

                    using var neoSystem = new Neo.NeoSystem(ProtocolSettings, storageProvider.Name);
                    var rpcSettings = Neo.Plugins.RpcServerSettings.Default with
                    {
                        BindAddress = IPAddress.Loopback,
                        Network = ProtocolSettings.Magic,
                        Port = node.RpcPort,
                        // TODO: Make these configurable (https://github.com/neo-project/neo-express/issues/109)
                        // MaxGasInvoke = 0,
                        // MaxFee = 0,
                    };
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
        }

        public IExpressStore GetNodeStore(ExpressConsensusNode node, bool discard)
        {
            var folder = fileSystem.GetNodePath(node);

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

        public IExpressStore GetCheckpointStore(string checkPointPath)
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
            RocksDbStore.RestoreCheckpoint(checkPointPath, checkpointTempPath, chain.Magic, chain.AddressVersion, multiSigAccount.ScriptHash);
            return new CheckpointStore(RocksDbStore.OpenReadOnly(checkpointTempPath), true, folderCleanup);
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
            return new Node.OfflineNode(ProtocolSettings, RocksDbStore.Open(nodePath), node.Wallet, chain, offlineTrace);
        }
    }
}
