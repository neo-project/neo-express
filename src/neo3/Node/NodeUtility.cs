using Akka.Actor;
using Microsoft.Extensions.Configuration;
using Neo;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.Wallets;
using NeoExpress.Abstractions.Models;
using NeoExpress.Neo3.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using static Neo.Ledger.Blockchain;

namespace NeoExpress.Neo3.Node
{
    using StackItem = Neo.VM.Types.StackItem;

    internal static class NodeUtility
    {
        public static bool InitializeProtocolSettings(ExpressChain chain, uint secondsPerBlock = 0)
        {
            secondsPerBlock = secondsPerBlock == 0 ? 15 : secondsPerBlock;

            IEnumerable<KeyValuePair<string, string>> settings()
            {
                yield return new KeyValuePair<string, string>(
                    "ProtocolConfiguration:Magic", $"{chain.Magic}");
                yield return new KeyValuePair<string, string>(
                    "ProtocolConfiguration:MillisecondsPerBlock", $"{secondsPerBlock * 1000}");
                yield return new KeyValuePair<string, string>(
                    "ProtocolConfiguration:ValidatorsCount", $"{chain.ConsensusNodes.Count}");

                foreach (var (node, index) in chain.ConsensusNodes.Select((n, i) => (n, i)))
                {
                    var privateKey = node.Wallet.Accounts
                        .Select(a => a.PrivateKey)
                        .Distinct().Single().HexToBytes();
                    var encodedPublicKey = new KeyPair(privateKey).PublicKey
                        .EncodePoint(true).ToHexString();
                    yield return new KeyValuePair<string, string>(
                        $"ProtocolConfiguration:StandbyCommittee:{index}", encodedPublicKey);
                    yield return new KeyValuePair<string, string>(
                        $"ProtocolConfiguration:SeedList:{index}", $"{IPAddress.Loopback}:{node.TcpPort}");
                }
            }

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings())
                .Build();

            return ProtocolSettings.Initialize(config);
        }

        // TODO: replace with ManagementContract.ListContracts when https://github.com/neo-project/neo/pull/2134 is merged
        const byte ManagementContract_PREFIX = 8;
        static Lazy<byte[]> listContractsPrefix = new Lazy<byte[]>(() => new KeyBuilder(NativeContract.Management.Id, ManagementContract_PREFIX).ToArray());

        public static IEnumerable<(int Id, UInt160 Hash, ContractManifest Manifest)> ListContracts(StoreView snapshot)
            => snapshot.Storages.Find(listContractsPrefix.Value)
                .Select(kvp => kvp.Value.GetInteroperable<Neo.SmartContract.ContractState>())
                .Select(s => (s.Id, s.Hash, s.Manifest));


        public static Task RunAsync(IStore store, ExpressConsensusNode node, bool enableTrace, TextWriter writer, CancellationToken cancellationToken)
        {
            writer.WriteLine(store.GetType().Name);

            var tcs = new TaskCompletionSource<bool>();

            _ = Task.Run(() =>
            {
                try
                {
                    var wallet = DevWallet.FromExpressWallet(node.Wallet);
                    var multiSigAccount = node.Wallet.Accounts.Single(a => a.IsMultiSigContract());

                    var logPlugin = new LogPlugin(writer);
                    var storageProvider = new ExpressStorageProvider(store);
                    var appEngineProvider = enableTrace ? new ExpressApplicationEngineProvider() : null;
                    var appLogsPlugin = new ExpressAppLogsPlugin(store);

                    using var system = new NeoSystem(storageProvider.Name);
                    var rpcSettings = new Neo.Plugins.RpcServerSettings(port: node.RpcPort);
                    var rpcServer = new Neo.Plugins.RpcServer(system, rpcSettings);
                    var expressRpcServer = new ExpressRpcServer(multiSigAccount);
                    rpcServer.RegisterMethods(expressRpcServer);
                    rpcServer.RegisterMethods(appLogsPlugin);
                    rpcServer.StartRpcServer();

                    system.StartNode(new ChannelsConfig
                    {
                        Tcp = new IPEndPoint(IPAddress.Loopback, node.TcpPort),
                        WebSocket = new IPEndPoint(IPAddress.Loopback, node.WebSocketPort),
                    });
                    system.StartConsensus(wallet);

                    cancellationToken.WaitHandle.WaitOne();
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

            return tcs.Task;
        }
    }
}
