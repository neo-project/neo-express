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

        public async Task RunAsync(IStore store, ExpressConsensusNode node, bool enableTrace, IConsole console, CancellationToken token)
        {
            await console.Out.WriteLineAsync(store.GetType().Name).ConfigureAwait(false);

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

        public bool IsRunning(ExpressConsensusNode node)
        {
            // Check to see if there's a neo-express blockchain currently running by
            // attempting to open a mutex with the multisig account address for a name

            var account = node.Wallet.Accounts.Single(a => a.IsDefault);
            return Mutex.TryOpenExisting(GLOBAL_PREFIX + account.ScriptHash, out var _);
        }

        string GetNodePath(ExpressWalletAccount account)
        {
            if (fileSystem == null) throw new ArgumentNullException(nameof(fileSystem));
            if (account == null) throw new ArgumentNullException(nameof(account));

            var rootPath = fileSystem.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify),
                "Neo-Express", 
                "blockchain-nodes");
            return fileSystem.Path.Combine(rootPath, account.ScriptHash);
        }

        string GetNodePath(ExpressWallet wallet)
        {
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));

            var defaultAccount = wallet.Accounts.Single(a => a.IsDefault);
            return GetNodePath(defaultAccount);
        }

        public string GetNodePath(ExpressConsensusNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            return GetNodePath(node.Wallet);
        }
    }
}
