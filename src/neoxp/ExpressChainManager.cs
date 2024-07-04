// Copyright (C) 2015-2024 The Neo Project.
//
// ExpressChainManager.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Neo.Plugins.RpcServer;
using NeoExpress.Models;
using NeoExpress.Node;
using Nito.Disposables;
using System.IO.Abstractions;
using System.Net;

namespace NeoExpress
{
    internal class ExpressChainManager
    {
        const string GLOBAL_PREFIX = "Global\\";
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

        static bool IsNodeRunning(ExpressConsensusNode node)
        {
            // Check to see if there's a neo-express blockchain currently running by
            // attempting to open a mutex with the multisig account address for a name

            var account = node.Wallet.Accounts.Single(a => a.IsDefault);
            return Mutex.TryOpenExisting(GLOBAL_PREFIX + account.ScriptHash, out var _);
        }

        public bool IsRunning(ExpressConsensusNode? node = null)
        {
            if (node is null)
            {
                for (var i = 0; i < chain.ConsensusNodes.Count; i++)
                {
                    if (IsNodeRunning(chain.ConsensusNodes[i]))
                    {
                        return true;
                    }
                }
                return false;
            }
            else
            {
                return IsNodeRunning(node);
            }
        }

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

            var parentPath = fileSystem.Path.GetDirectoryName(checkpointPath)
                ?? throw new InvalidOperationException($"GetDirectoryName({checkpointPath}) returned null");
            if (!fileSystem.Directory.Exists(parentPath))
            {
                fileSystem.Directory.CreateDirectory(parentPath);
            }

            var mode = await expressNode.CreateCheckpointAsync(checkpointPath).ConfigureAwait(false);

            if (writer is not null)
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
            if (IsNodeRunning(node))
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

        public void SaveChain(string path)
        {
            fileSystem.SaveChain(chain, path);
        }

        public void ResetNode(ExpressConsensusNode node, bool force)
        {
            if (IsNodeRunning(node))
            {
                var scriptHash = node.Wallet.DefaultAccount?.ScriptHash ?? "<unknown>";
                throw new InvalidOperationException($"node {scriptHash} currently running");
            }

            var nodePath = fileSystem.GetNodePath(node);
            if (fileSystem.Directory.Exists(nodePath))
            {
                if (!force)
                {
                    throw new InvalidOperationException("--force must be specified when resetting a node");
                }

                fileSystem.Directory.Delete(nodePath, true);
            }
        }

        public async Task<bool> StopNodeAsync(ExpressConsensusNode node)
        {
            if (!IsNodeRunning(node))
                return false;

            var rpcClient = new Neo.Network.RPC.RpcClient(new Uri($"http://localhost:{node.RpcPort}"), protocolSettings: ProtocolSettings);
            var json = await rpcClient.RpcSendAsync("expressshutdown").ConfigureAwait(false);
            var processId = int.Parse(json["process-id"]!.AsString());
            var process = System.Diagnostics.Process.GetProcessById(processId);
            await process.WaitForExitAsync().ConfigureAwait(false);
            return true;
        }

        public async Task RunAsync(IExpressStorage expressStorage, ExpressConsensusNode node, bool enableTrace, IConsole console, CancellationToken token)
        {
            if (IsNodeRunning(node))
            {
                throw new Exception("Node already running");
            }

            var tcs = new TaskCompletionSource<bool>();
            _ = Task.Run(() =>
            {
                try
                {
                    var defaultAccount = node.Wallet.Accounts.Single(a => a.IsDefault);
                    using var mutex = new Mutex(true, GLOBAL_PREFIX + defaultAccount.ScriptHash);

                    var wallet = DevWallet.FromExpressWallet(ProtocolSettings, node.Wallet);
                    var multiSigAccount = wallet.GetMultiSigAccounts().Single();

                    var storeProvider = new ExpressStoreProvider(expressStorage);
                    Neo.Persistence.StoreFactory.RegisterProvider(storeProvider);
                    if (enableTrace)
                    { Neo.SmartContract.ApplicationEngine.Provider = new ExpressApplicationEngineProvider(); }

                    using var persistencePlugin = new ExpressPersistencePlugin();
                    using var logPlugin = new ExpressLogPlugin(console);
                    using var dbftPlugin = new Neo.Plugins.DBFTPlugin.DBFTPlugin(GetConsensusSettings(chain));
                    using var rpcServerPlugin = new ExpressRpcServerPlugin(GetRpcServerSettings(chain, node),
                        expressStorage, multiSigAccount.ScriptHash);
                    using var neoSystem = new Neo.NeoSystem(ProtocolSettings, storeProvider.Name);

                    neoSystem.StartNode(new Neo.Network.P2P.ChannelsConfig
                    {
                        Tcp = new IPEndPoint(IPAddress.Loopback, node.TcpPort)
                    });
                    dbftPlugin.Start(wallet);

                    // DevTracker looks for a string that starts with "Neo express is running" to confirm that the instance has started
                    // Do not remove or re-word this console output:
                    console.Out.WriteLine($"Neo express is running ({expressStorage.Name})");

                    var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(token, rpcServerPlugin.CancellationToken);
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

            static Neo.Plugins.DBFTPlugin.Settings GetConsensusSettings(ExpressChain chain)
            {
                var settings = new Dictionary<string, string>()
                {
                    { "PluginConfiguration:Network", $"{chain.Network}" },
                    { "IgnoreRecoveryLogs", "true" }
                };

                var config = new ConfigurationBuilder().AddInMemoryCollection(settings!).Build();
                return new Neo.Plugins.DBFTPlugin.Settings(config.GetSection("PluginConfiguration"));
            }

            static RpcServerSettings GetRpcServerSettings(ExpressChain chain, ExpressConsensusNode node)
            {
                var ipAddress = chain.TryReadSetting<IPAddress>("rpc.BindAddress", IPAddress.TryParse, out var bindAddress)
                    ? bindAddress : IPAddress.Loopback;

                var settings = new Dictionary<string, string>()
                {
                    { "PluginConfiguration:Network", $"{chain.Network}" },
                    { "PluginConfiguration:BindAddress", $"{ipAddress}" },
                    { "PluginConfiguration:Port", $"{node.RpcPort}" },
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

                var sessionEnabled = !chain.TryReadSetting<bool>("rpc.SessionEnabled", bool.TryParse, out var value) || value;
                settings.Add("PluginConfiguration:SessionEnabled", $"{sessionEnabled}");

                var config = new ConfigurationBuilder().AddInMemoryCollection(settings!).Build();
                return RpcServerSettings.Load(config.GetSection("PluginConfiguration"));
            }
        }

        public IExpressStorage GetNodeStorageProvider(ExpressConsensusNode node, bool discard)
        {
            var nodePath = fileSystem.GetNodePath(node);
            if (!fileSystem.Directory.Exists(nodePath))
                fileSystem.Directory.CreateDirectory(nodePath);
            return discard
                ? CheckpointExpressStorage.OpenForDiscard(nodePath)
                : new RocksDbExpressStorage(nodePath);
        }

        public IExpressStorage GetCheckpointStorageProvider(string checkPointPath)
        {
            if (chain.ConsensusNodes.Count != 1)
            {
                throw new ArgumentException("Checkpoint restore is only supported on single node express instances", nameof(chain));
            }

            var node = chain.ConsensusNodes[0];
            if (IsNodeRunning(node))
                throw new Exception($"node already running");

            checkPointPath = ResolveCheckpointFileName(checkPointPath);
            if (!fileSystem.File.Exists(checkPointPath))
            {
                throw new Exception($"Checkpoint {checkPointPath} couldn't be found");
            }

            var wallet = DevWallet.FromExpressWallet(ProtocolSettings, node.Wallet);
            var multiSigAccount = wallet.GetMultiSigAccounts().Single();

            return CheckpointExpressStorage.OpenCheckpoint(checkPointPath, scriptHash: multiSigAccount.ScriptHash);
        }

        OfflineNode GetOfflineNode(bool offlineTrace = false)
        {
            if (IsRunning())
                throw new NotSupportedException("Cannot get offline node while chain is running");

            var node = chain.ConsensusNodes[0];
            var nodePath = fileSystem.GetNodePath(node);
            if (!fileSystem.Directory.Exists(nodePath))
                fileSystem.Directory.CreateDirectory(nodePath);

            return new Node.OfflineNode(ProtocolSettings,
                new RocksDbExpressStorage(nodePath),
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
                if (IsNodeRunning(consensusNode))
                {
                    return new Node.OnlineNode(ProtocolSettings, chain, consensusNode);
                }
            }

            return GetOfflineNode(offlineTrace);
        }

        public ExpressWallet CreateWallet(string name, string privateKey, bool overwrite = false)
        {
            if (chain.IsReservedName(name))
            {
                throw new Exception($"{name} is a reserved name. Choose a different wallet name.");
            }

            var existingWallet = chain.GetWallet(name);
            if (existingWallet is not null)
            {
                if (!overwrite)
                {
                    throw new Exception($"{name} dev wallet already exists. Use --force to overwrite.");
                }

                chain.Wallets.Remove(existingWallet);
            }

            byte[]? priKey = null;
            if (string.IsNullOrEmpty(privateKey) == false)
            {
                try
                {
                    if (privateKey.StartsWith('L'))
                        priKey = Neo.Wallets.Wallet.GetPrivateKeyFromWIF(privateKey);
                    else
                        priKey = Convert.FromHexString(privateKey);
                }
                catch (FormatException)
                {
                    throw new FormatException("Private key must be in HEX or WIF format.");
                }
            }

            var wallet = new DevWallet(ProtocolSettings, name);
            var account = priKey == null ? wallet.CreateAccount() : wallet.CreateAccount(priKey!);
            account.IsDefault = true;

            var expressWallet = wallet.ToExpressWallet();
            chain.Wallets ??= new List<ExpressWallet>(1);
            chain.Wallets.Add(expressWallet);
            return expressWallet;
        }
    }
}
