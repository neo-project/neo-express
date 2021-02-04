using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Neo.Network.RPC;
using Neo.Persistence;
using NeoExpress.Models;
using Newtonsoft.Json;
using Nito.Disposables;

namespace NeoExpress
{
    internal class ExpressChainManager : IExpressChainManager
    {
        const string GLOBAL_PREFIX = "Global\\";
        const string CHECKPOINT_EXTENSION = ".neoxp-checkpoint";

        readonly IFileSystem fileSystem;
        readonly ExpressChain chain;

        public ExpressChainManager(IFileSystem fileSystem, ExpressChain chain)
        {
            this.fileSystem = fileSystem;
            this.chain = chain;
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

        public async Task<(string path, bool online)> CreateCheckpointAsync(string checkPointPath, bool force)
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

            var node = chain.ConsensusNodes[0];
            if (IsRunning(node))
            {
                var uri = chain.GetUri();
                var rpcClient = new RpcClient(uri);
                await rpcClient.RpcSendAsync("expresscreatecheckpoint", checkPointPath).ConfigureAwait(false);
                return (checkPointPath, true);
            }
            else
            {
                var multiSigAccount = node.Wallet.Accounts.Single(a => a.IsMultiSigContract());
                using var db = RocksDbStore.Open(fileSystem.GetNodePath(node));
                db.CreateCheckpoint(checkPointPath, chain.Magic, multiSigAccount.ScriptHash);
                return (checkPointPath, false);
            }
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

            var multiSigAccount = node.Wallet.Accounts.Single(a => a.IsMultiSigContract());
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

            RocksDbStore.RestoreCheckpoint(checkPointArchive, checkpointTempPath, chain.Magic, multiSigAccount.ScriptHash);
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

            chain.InitalizeProtocolSettings(secondsPerBlock);

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

                    var dbftPlugin = new Neo.Consensus.DBFTPlugin();
                    var logPlugin = new Node.LogPlugin(writer);
                    var storageProvider = new Node.ExpressStorageProvider((IStore)store);
                    var appEngineProvider = enableTrace ? new Node.ExpressApplicationEngineProvider() : null;
                    var appLogsPlugin = new Node.ExpressAppLogsPlugin(store);

                    using var system = new Neo.NeoSystem(storageProvider.Name);
                    var rpcSettings = new Neo.Plugins.RpcServerSettings(port: node.RpcPort);
                    var rpcServer = new Neo.Plugins.RpcServer(system, rpcSettings);
                    // var expressRpcServer = new Node.ExpressRpcServer(multiSigAccount);
                    // rpcServer.RegisterMethods(expressRpcServer);
                    rpcServer.RegisterMethods(appLogsPlugin);
                    rpcServer.StartRpcServer();

                    system.StartNode(new Neo.Network.P2P.ChannelsConfig
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

            var multiSigAccount = node.Wallet.Accounts.Single(a => a.IsMultiSigContract());
            RocksDbStore.RestoreCheckpoint(checkPointPath, checkpointTempPath, chain.Magic, multiSigAccount.ScriptHash);
            return new CheckpointStore(RocksDbStore.OpenReadOnly(checkpointTempPath), true, folderCleanup);
        }

        public IExpressNode GetExpressNode(bool offlineTrace = false)
        {
            // Check to see if there's a neo-express blockchain currently running by
            // attempting to open a mutex with the multisig account address for a name

            chain.InitalizeProtocolSettings();

            for (int i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var consensusNode = chain.ConsensusNodes[i];
                if (IsRunning(consensusNode))
                {
                    return new Node.OnlineNode(chain, consensusNode);
                }
            }

            var node = chain.ConsensusNodes[0];
            var nodePath = fileSystem.GetNodePath(node);
            if (!fileSystem.Directory.Exists(nodePath)) fileSystem.Directory.CreateDirectory(nodePath);
            return new Node.OfflineNode(RocksDbStore.Open(nodePath), node.Wallet, chain, offlineTrace);
        }
    }
}
