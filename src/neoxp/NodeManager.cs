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
using NeoExpress.Node;

namespace NeoExpress
{
    class NodeManager : INodeManager
    {
        readonly IFileSystem fileSystem;

        public NodeManager(IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public async Task RunAsync(IStore store, ExpressChain chain, ExpressConsensusNode node, uint secondsPerBlock, bool enableTrace, IConsole console, CancellationToken token)
        {
            if (IsRunning(node))
            {
                throw new Exception("Node already running");
            }
            
            await console.Out.WriteLineAsync(store.GetType().Name).ConfigureAwait(false);

            chain.InitalizeProtocolSettings(secondsPerBlock);

            var tcs = new TaskCompletionSource<bool>();
            _ = Task.Run(() =>
            {
                try
                {
                    var defaultAccount = node.Wallet.Accounts.Single(a => a.IsDefault);
                    using var mutex = new Mutex(true, GLOBAL_PREFIX + defaultAccount.ScriptHash);

                    var wallet = DevWallet.FromExpressWallet(node.Wallet);
                    var multiSigAccount = node.Wallet.Accounts.Single(a => a.IsMultiSigContract());

                    var logPlugin = new LogPlugin(console.Out);
                    var storageProvider = new ExpressStorageProvider(store);
                    var appEngineProvider = enableTrace ? new ExpressApplicationEngineProvider() : null;
                    var appLogsPlugin = new ExpressAppLogsPlugin(store);

                    using var system = new Neo.NeoSystem(storageProvider.Name);
                    var rpcSettings = new Neo.Plugins.RpcServerSettings(port: node.RpcPort);
                    var rpcServer = new Neo.Plugins.RpcServer(system, rpcSettings);
                    var expressRpcServer = new ExpressRpcServer(multiSigAccount);
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

        public void Reset(ExpressConsensusNode node, bool force)
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
    }
}
