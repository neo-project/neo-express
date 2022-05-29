using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.Consensus;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.VM;
using NeoExpress.Models;
using static Neo.Ledger.Blockchain;

namespace NeoExpress.Node
{
    partial class ExpressSystem : IDisposable
    {
        internal const string GLOBAL_PREFIX = "Global\\";

        readonly ExpressChain chain;
        readonly ExpressConsensusNode node;
        readonly IExpressStorage expressStorage;
        readonly NeoSystem neoSystem;
        readonly ApplicationEngineProviderPlugin? appEngineProviderPlugin;
        readonly ConsolePlugin consolePlugin;
        readonly DBFTPlugin dbftPlugin;
        readonly PersistenceWrapper persistenceWrapper;
        readonly StorageProviderPlugin storageProviderPlugin;
        readonly WebServerPlugin webServerPlugin;

        public void Dispose()
        {
            neoSystem.Dispose();
        }

        public ExpressSystem(ExpressChain chain, ExpressConsensusNode node, IExpressStorage expressStorage, IConsole console, bool trace, uint? secondsPerBlock)
        {
            this.chain = chain;
            this.node = node;
            this.expressStorage = expressStorage;

            var _secondsPerBlock = secondsPerBlock.HasValue
                ? secondsPerBlock.Value
                : chain.TryReadSetting<uint>("chain.SecondsPerBlock", uint.TryParse, out var secondsPerBlockSetting)
                    ? secondsPerBlockSetting
                    : 0;
            var settings = chain.GetProtocolSettings(_secondsPerBlock);

            consolePlugin = new ConsolePlugin(console);
            appEngineProviderPlugin = trace ? new ApplicationEngineProviderPlugin() : null;
            storageProviderPlugin = new StorageProviderPlugin(expressStorage);
            persistenceWrapper = new PersistenceWrapper(this.OnPersist);
            webServerPlugin = new WebServerPlugin(chain, node);
            dbftPlugin = new DBFTPlugin(GetConsensusSettings(chain));
            neoSystem = new NeoSystem(settings, storageProviderPlugin.Name);
        }

        public async Task RunAsync(CancellationToken token)
        {
            if (node.IsRunning()) { throw new Exception("Node already running"); }

            var tcs = new TaskCompletionSource<bool>();
            _ = Task.Run(() =>
            {
                try
                {
                    var wallet = DevWallet.FromExpressWallet(neoSystem.Settings, node.Wallet);
                    var defaultAccount = node.Wallet.Accounts.Single(a => a.IsDefault);

                    using var mutex = new Mutex(true, GLOBAL_PREFIX + defaultAccount.ScriptHash);

                    neoSystem.StartNode(new Neo.Network.P2P.ChannelsConfig
                    {
                        Tcp = new IPEndPoint(IPAddress.Loopback, node.TcpPort),
                        WebSocket = new IPEndPoint(IPAddress.Loopback, node.WebSocketPort),
                    });
                    webServerPlugin.Start();
                    dbftPlugin.Start(wallet);

                    // DevTracker looks for a string that starts with "Neo express is running" to confirm that the instance has started
                    // Do not remove or re-word this console output:
                    consolePlugin.WriteLine($"Neo express is running ({expressStorage.Name})");

                    var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(token, webServerPlugin.CancellationToken);
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
        }

        private void OnPersist(Block block, IReadOnlyList<ApplicationExecuted> appExecutions)
        {
            if (appExecutions.Count > ushort.MaxValue) throw new Exception("applicationExecutedList too big");

            using var appLogsSnapshot = expressStorage.AppLogs.GetSnapshot();
            using var notificationsSnapshot = expressStorage.Notifications.GetSnapshot();

            var notificationIndex = new byte[sizeof(uint) + (2 * sizeof(ushort))];
            BinaryPrimitives.WriteUInt32BigEndian(
                notificationIndex.AsSpan(0, sizeof(uint)),
                block.Index);

            for (int i = 0; i < appExecutions.Count; i++)
            {
                ApplicationExecuted appExec = appExecutions[i];
                if (appExec.Transaction == null) continue;

                var txJson = TxLogToJson(appExec);
                appLogsSnapshot.Put(appExec.Transaction.Hash.ToArray(), Neo.Utility.StrictUTF8.GetBytes(txJson.ToString()));

                if (appExec.VMState != VMState.FAULT)
                {
                    if (appExec.Notifications.Length > ushort.MaxValue) throw new Exception("appExec.Notifications too big");

                    BinaryPrimitives.WriteUInt16BigEndian(notificationIndex.AsSpan(sizeof(uint), sizeof(ushort)), (ushort)i);

                    for (int j = 0; j < appExec.Notifications.Length; j++)
                    {
                        BinaryPrimitives.WriteUInt16BigEndian(
                            notificationIndex.AsSpan(sizeof(uint) + sizeof(ushort), sizeof(ushort)),
                            (ushort)j);
                        var record = new NotificationRecord(appExec.Notifications[j]);
                        notificationsSnapshot.Put(notificationIndex, record.ToArray());
                    }
                }
            }

            var blockJson = BlockLogToJson(block, appExecutions);
            if (blockJson != null)
            {
                appLogsSnapshot.Put(block.Hash.ToArray(), Neo.Utility.StrictUTF8.GetBytes(blockJson.ToString()));
            }

            appLogsSnapshot.Commit();
            notificationsSnapshot.Commit();
        }

        static Neo.Consensus.Settings GetConsensusSettings(ExpressChain chain)
        {
            var settings = new Dictionary<string, string>()
                {
                    { "PluginConfiguration:Network", $"{chain.Network}" }
                };

            var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            return new Neo.Consensus.Settings(config.GetSection("PluginConfiguration"));
        }

        // TxLogToJson and BlockLogToJson copied from Neo.Plugins.LogReader in the ApplicationLogs plugin
        // to avoid dependency on LevelDBStore package

        public static JObject TxLogToJson(Blockchain.ApplicationExecuted appExec)
        {
            global::System.Diagnostics.Debug.Assert(appExec.Transaction != null);

            var txJson = new JObject();
            txJson["txid"] = appExec.Transaction.Hash.ToString();
            JObject trigger = new JObject();
            trigger["trigger"] = appExec.Trigger;
            trigger["vmstate"] = appExec.VMState;
            trigger["exception"] = appExec.Exception?.GetBaseException().Message;
            trigger["gasconsumed"] = appExec.GasConsumed.ToString();
            try
            {
                trigger["stack"] = appExec.Stack.Select(q => q.ToJson()).ToArray();
            }
            catch (InvalidOperationException)
            {
                trigger["stack"] = "error: recursive reference";
            }
            trigger["notifications"] = appExec.Notifications.Select(q =>
            {
                JObject notification = new JObject();
                notification["contract"] = q.ScriptHash.ToString();
                notification["eventname"] = q.EventName;
                try
                {
                    notification["state"] = q.State.ToJson();
                }
                catch (InvalidOperationException)
                {
                    notification["state"] = "error: recursive reference";
                }
                return notification;
            }).ToArray();

            txJson["executions"] = new List<JObject>() { trigger }.ToArray();
            return txJson;
        }

        public static JObject? BlockLogToJson(Block block, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            var blocks = applicationExecutedList.Where(p => p.Transaction is null).ToArray();
            if (blocks.Length > 0)
            {
                var blockJson = new JObject();
                var blockHash = block.Hash.ToArray();
                blockJson["blockhash"] = block.Hash.ToString();
                var triggerList = new List<JObject>();
                foreach (var appExec in blocks)
                {
                    JObject trigger = new JObject();
                    trigger["trigger"] = appExec.Trigger;
                    trigger["vmstate"] = appExec.VMState;
                    trigger["gasconsumed"] = appExec.GasConsumed.ToString();
                    try
                    {
                        trigger["stack"] = appExec.Stack.Select(q => q.ToJson()).ToArray();
                    }
                    catch (InvalidOperationException)
                    {
                        trigger["stack"] = "error: recursive reference";
                    }
                    trigger["notifications"] = appExec.Notifications.Select(q =>
                    {
                        JObject notification = new JObject();
                        notification["contract"] = q.ScriptHash.ToString();
                        notification["eventname"] = q.EventName;
                        try
                        {
                            notification["state"] = q.State.ToJson();
                        }
                        catch (InvalidOperationException)
                        {
                            notification["state"] = "error: recursive reference";
                        }
                        return notification;
                    }).ToArray();
                    triggerList.Add(trigger);
                }
                blockJson["executions"] = triggerList.ToArray();
                return blockJson;
            }

            return null;
        }
    }
}
