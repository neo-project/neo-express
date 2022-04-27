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
using Neo.BlockchainToolkit.SmartContract;
using Neo.Ledger;
using Neo.Plugins;
using NeoExpress.Models;
using NeoExpress.Node;
using Nito.Disposables;

namespace NeoExpress
{
    internal class ExpressChainManager
    {
        internal const string GLOBAL_PREFIX = "Global\\";
        const string CHECKPOINT_EXTENSION = ".neoxp-checkpoint";

        readonly IFileSystem fileSystem;
        readonly ExpressChain chain;
        public ProtocolSettings ProtocolSettings { get; }

        public ExpressChainManager(IFileSystem fileSystem, ExpressChain chain, uint? secondsPerBlock = null)
        {
            this.fileSystem = fileSystem;
            this.chain = chain;

            uint secondsPerBlockResult = secondsPerBlock.HasValue
                ? secondsPerBlock.Value
                : chain.TryReadSetting<uint>("chain.SecondsPerBlock", uint.TryParse, out var secondsPerBlockSetting)
                    ? secondsPerBlockSetting
                    : 0;

            this.ProtocolSettings = chain.GetProtocolSettings(secondsPerBlockResult);
        }

        public ExpressChain Chain => chain;

        string ResolveCheckpointFileName(string path) => fileSystem.ResolveFileName(path, CHECKPOINT_EXTENSION, () => $"{DateTimeOffset.Now:yyyyMMdd-hhmmss}");

        public async Task<(string path, IExpressNode.CheckpointMode checkpointMode)> CreateCheckpointAsync(IExpressNode expressNode, string checkpointPath, bool force, System.IO.TextWriter? writer = null)
        {
            if (chain.ConsensusNodes.Count != 1)
            {
                throw new ArgumentException("Checkpoint create is only supported on single node express instances", nameof(chain));
            }

            checkpointPath = ResolveCheckpointFileName(checkpointPath);
            if (fileSystem.File.Exists(checkpointPath))
            {
                if (force)
                {
                    fileSystem.File.Delete(checkpointPath);
                }
                else
                {
                    throw new Exception("You must specify --force to overwrite an existing file");
                }
            }

            var parentPath = fileSystem.Path.GetDirectoryName(checkpointPath);
            if (!fileSystem.Directory.Exists(parentPath))
            {
                fileSystem.Directory.CreateDirectory(parentPath);
            }

            var mode = await expressNode.CreateCheckpointAsync(checkpointPath).ConfigureAwait(false);

            if (writer != null)
            {
                await writer.WriteLineAsync($"Created {fileSystem.Path.GetFileName(checkpointPath)} checkpoint {mode}").ConfigureAwait(false);
            }

            return (checkpointPath, mode);
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
            if (node.IsRunning())
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

            var nodePath = fileSystem.GetNodePath(node);
            if (fileSystem.Directory.Exists(nodePath))
            {
                if (!force)
                {
                    throw new Exception("You must specify force to restore a checkpoint to an existing blockchain.");
                }

                fileSystem.Directory.Delete(nodePath, true);
            }

            var wallet = DevWallet.FromExpressWallet(ProtocolSettings, node.Wallet);
            var multiSigAccount = wallet.GetMultiSigAccounts().Single();
            RocksDbUtility.RestoreCheckpoint(checkPointArchive, checkpointTempPath,
                ProtocolSettings.Network, ProtocolSettings.AddressVersion, multiSigAccount.ScriptHash);
            fileSystem.Directory.Move(checkpointTempPath, nodePath);
        }

        public async Task RunAsync(IStorageProvider store, ExpressConsensusNode node, bool enableTrace, TextWriter writer, CancellationToken token)
        {
            if (node.IsRunning())
            {
                throw new Exception("Node already running");
            }

            var tcs = new TaskCompletionSource<bool>();
            _ = Task.Run(() =>
            {
                try
                {
                    var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(token);

                    var defaultAccount = node.Wallet.Accounts.Single(a => a.IsDefault);
                    using var mutex = new Mutex(true, GLOBAL_PREFIX + defaultAccount.ScriptHash);

                    var wallet = DevWallet.FromExpressWallet(ProtocolSettings, node.Wallet);
                    var multiSigAccount = wallet.GetMultiSigAccounts().Single();

                    var logPlugin = new Node.LogPlugin(writer);
                    var storageProviderPlugin = new Node.StorageProviderPlugin(store);
                    var appEngineProvider = enableTrace ? new Node.ApplicationEngineProvider() : null;
                    var dbftPlugin = new Neo.Consensus.DBFTPlugin(GetConsensusSettings(chain));
                    var persistencePlugin = new Node.PersistencePlugin(store);

                    using var neoSystem = new Neo.NeoSystem(ProtocolSettings, storageProviderPlugin.Name);
                    _ = neoSystem.ActorSystem.ActorOf(EventWrapper<Blockchain.ApplicationExecuted>.Props(OnApplicationExecuted));
                    var rpcSettings = GetRpcServerSettings(chain, node);
                    var rpcServer = new Neo.Plugins.RpcServer(neoSystem, rpcSettings);
                    var expressRpcMethods = new ExpressRpcMethods(neoSystem, store, multiSigAccount.ScriptHash, linkedToken);
                    rpcServer.RegisterMethods(expressRpcMethods);
                    rpcServer.RegisterMethods(persistencePlugin);
                    rpcServer.StartRpcServer();

                    neoSystem.StartNode(new Neo.Network.P2P.ChannelsConfig
                    {
                        Tcp = new IPEndPoint(IPAddress.Loopback, node.TcpPort),
                        WebSocket = new IPEndPoint(IPAddress.Loopback, node.WebSocketPort),
                    });
                    dbftPlugin.Start(wallet);

                    // DevTracker looks for a string that starts with "Neo express is running" to confirm that the instance has started
                    // Do not remove or re-word this console output:
                    writer.WriteLine($"Neo express is running ({store.GetType().Name})");

                    linkedToken.Token.WaitHandle.WaitOne();
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
                    { "PluginConfiguration:Network", $"{chain.Network}" }
                };

                var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
                return new Neo.Consensus.Settings(config.GetSection("PluginConfiguration"));
            }

            static RpcServerSettings GetRpcServerSettings(ExpressChain chain, ExpressConsensusNode node)
            {
                var ipAddress = chain.TryReadSetting<IPAddress>("rpc.BindAddress", IPAddress.TryParse, out var bindAddress)
                    ? bindAddress : IPAddress.Loopback;

                var settings = new Dictionary<string, string>()
                {
                    { "PluginConfiguration:Network", $"{chain.Network}" },
                    { "PluginConfiguration:BindAddress", $"{ipAddress}" },
                    { "PluginConfiguration:Port", $"{node.RpcPort}" }
                };

                if (chain.TryReadSetting<decimal>("rpc.MaxGasInvoke", decimal.TryParse, out var maxGasInvoke))
                {
                    settings.Add("PluginConfiguration:MaxGasInvoke", $"{maxGasInvoke}");
                }

                if (chain.TryReadSetting<decimal>("rpc.MaxFee", decimal.TryParse, out var maxFee))
                {
                    settings.Add("PluginConfiguration:MaxFee", $"{maxFee}");
                }

                if (chain.TryReadSetting<int>("rpc.MaxIteratorResultItems", int.TryParse, out var maxIteratorResultItems)
                    && maxIteratorResultItems > 0)
                {
                    settings.Add("PluginConfiguration:MaxIteratorResultItems", $"{maxIteratorResultItems}");
                }

                var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
                return RpcServerSettings.Load(config.GetSection("PluginConfiguration"));
            }
        }

        void OnApplicationExecuted(Blockchain.ApplicationExecuted applicationExecuted)
        {
            if (applicationExecuted.VMState == Neo.VM.VMState.FAULT)
            {
                var logMessage = $"Tx FAULT: hash={applicationExecuted.Transaction.Hash}";
                if (!string.IsNullOrEmpty(applicationExecuted.Exception.Message))
                {
                    logMessage += $" exception=\"{applicationExecuted.Exception.Message}\"";
                }
                Console.Error.WriteLine($"\x1b[31m{logMessage}\x1b[0m");
            }
        }

        public IStorageProvider GetNodeStorageProvider(ExpressConsensusNode node, bool discard)
        {
            var nodePath = fileSystem.GetNodePath(node);
            if (!fileSystem.Directory.Exists(nodePath)) fileSystem.Directory.CreateDirectory(nodePath);

            return discard
                ? RocksDbStorageProvider.OpenForDiscard(nodePath)
                : RocksDbStorageProvider.Open(nodePath);
        }

        public IStorageProvider GetCheckpointStorageProvider(string checkPointPath)
        {
            if (chain.ConsensusNodes.Count != 1)
            {
                throw new ArgumentException("Checkpoint restore is only supported on single node express instances", nameof(chain));
            }

            var node = chain.ConsensusNodes[0];
            if (node.IsRunning()) throw new Exception($"node already running");

            checkPointPath = ResolveCheckpointFileName(checkPointPath);
            if (!fileSystem.File.Exists(checkPointPath))
            {
                throw new Exception($"Checkpoint {checkPointPath} couldn't be found");
            }

            var wallet = DevWallet.FromExpressWallet(ProtocolSettings, node.Wallet);
            var multiSigAccount = wallet.GetMultiSigAccounts().Single();

            return CheckpointStorageProvider.Open(checkPointPath, scriptHash: multiSigAccount.ScriptHash);
        }

        OfflineNode GetOfflineNode(bool offlineTrace = false)
        {
            var node = chain.ConsensusNodes[0];
            var nodePath = fileSystem.GetNodePath(node);
            if (!fileSystem.Directory.Exists(nodePath)) fileSystem.Directory.CreateDirectory(nodePath);

            return new Node.OfflineNode(ProtocolSettings,
                RocksDbStorageProvider.Open(nodePath),
                node.Wallet,
                chain,
                offlineTrace);
        }

        public IExpressNode GetExpressNode(bool offlineTrace = false)
        {
            // Check to see if there's a neo-express blockchain currently running by
            // attempting to open a mutex with the multisig account address for a name

            for (int i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var consensusNode = chain.ConsensusNodes[i];
                if (consensusNode.IsRunning())
                {
                    return new Node.OnlineNode(ProtocolSettings, chain, consensusNode);
                }
            }

            return GetOfflineNode(offlineTrace);
        }
    }
}
