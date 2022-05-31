using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Consensus;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using NeoExpress.Models;
using static Neo.Ledger.Blockchain;

namespace NeoExpress.Node
{
    partial class ExpressSystem : IDisposable
    {
        public const string GLOBAL_PREFIX = "Global\\";
        public const string APP_LOGS_STORE_NAME = "app-logs-store";
        public const string CONSENSUS_STATE_STORE_NAME = "ConsensusState";
        public const string NOTIFICATIONS_COLUMN_STORE_NAME = "notifications-store";

        readonly IExpressChain chain;
        readonly ExpressConsensusNode node;
        readonly IExpressStorage expressStorage;
        readonly NeoSystem neoSystem;
        readonly ApplicationEngineProviderPlugin? appEngineProviderPlugin;
        readonly ConsolePlugin consolePlugin;
        readonly DBFTPlugin dbftPlugin;
        readonly PersistenceWrapper persistenceWrapper;
        readonly StorageProviderPlugin storageProviderPlugin;
        readonly WebServerPlugin webServerPlugin;

        public ProtocolSettings ProtocolSettings => neoSystem.Settings;
        public Neo.Persistence.DataCache StoreView => neoSystem.StoreView;

        public ExpressSystem(IExpressChain chain, ExpressConsensusNode node, IExpressStorage expressStorage, bool trace, uint? secondsPerBlock)
        {
            this.chain = chain;
            this.node = node;
            this.expressStorage = expressStorage;

            consolePlugin = new ConsolePlugin();
            appEngineProviderPlugin = trace ? new ApplicationEngineProviderPlugin() : null;
            storageProviderPlugin = new StorageProviderPlugin(expressStorage);
            persistenceWrapper = new PersistenceWrapper(this.OnPersist);
            webServerPlugin = new WebServerPlugin(chain, node);
            webServerPlugin.RegisterMethods(this);
            dbftPlugin = new DBFTPlugin(GetConsensusSettings(chain));

            var _secondsPerBlock = secondsPerBlock.HasValue
                ? secondsPerBlock.Value
                : this.chain.TryReadSetting("chain.SecondsPerBlock", uint.TryParse, out uint secondsPerBlockSetting)
                    ? secondsPerBlockSetting
                    : 0;
            var settings = chain.GetProtocolSettings(_secondsPerBlock);
            neoSystem = new NeoSystem(settings, storageProviderPlugin.Name);
        }

        public void Dispose()
        {
            neoSystem.Dispose();
            expressStorage.Dispose();
        }

        void OnPersist(Block block, IReadOnlyList<ApplicationExecuted> appExecutions)
        {
            if (appExecutions.Count > ushort.MaxValue) throw new Exception("applicationExecutedList too big");

            using var appLogsSnapshot = expressStorage.AppLogsStore.GetSnapshot();
            using var notificationsSnapshot = expressStorage.NotificationsStore.GetSnapshot();

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

            // TxLogToJson and BlockLogToJson copied from Neo.Plugins.LogReader in the ApplicationLogs plugin
            // to avoid dependency on LevelDBStore package

            static JObject TxLogToJson(Blockchain.ApplicationExecuted appExec)
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

            static JObject? BlockLogToJson(Block block, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
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

        public IEnumerable<(UInt160 hash, ContractManifest manifest)> ListContracts()
        {
            return NativeContract.ContractManagement.ListContracts(neoSystem.StoreView)
                .OrderBy(c => c.Id)
                .Select(c => (c.Hash, c.Manifest));
        }












        public Neo.Persistence.SnapshotCache GetSnapshot() => neoSystem.GetSnapshot();
        public Task<RelayResult> RelayBlockAsync(Block block) => neoSystem.Blockchain.Ask<RelayResult>(block);

        public JObject? GetAppLog(UInt256 hash)
        {
            var value = expressStorage.AppLogsStore.TryGet(hash.ToArray());
            return value != null && value.Length != 0
                ? JObject.Parse(Neo.Utility.StrictUTF8.GetString(value))
                : null;
        }

        static Lazy<byte[]> backwardsNotificationsPrefix = new Lazy<byte[]>(() =>
        {
            var buffer = new byte[sizeof(uint) + sizeof(ushort)];
            BinaryPrimitives.WriteUInt32BigEndian(buffer, uint.MaxValue);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(sizeof(uint)), ushort.MaxValue);
            return buffer;
        });

        public IEnumerable<(uint blockIndex, ushort txIndex, NotificationRecord notification)> GetNotifications(
            SeekDirection direction, IReadOnlySet<UInt160>? contracts, string eventName)
                => GetNotifications(direction, contracts,
                    string.IsNullOrEmpty(eventName) ? null : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { eventName });

        public IEnumerable<(uint blockIndex, ushort txIndex, NotificationRecord notification)> GetNotifications(
            SeekDirection direction = SeekDirection.Forward,
            IReadOnlySet<UInt160>? contracts = null,
            IReadOnlySet<string>? eventNames = null)
        {
            var prefix = direction == SeekDirection.Forward
                ? Array.Empty<byte>()
                : backwardsNotificationsPrefix.Value;

            return expressStorage.NotificationsStore.Seek(prefix, direction)
                .Select(t => ParseNotification(t.Key, t.Value))
                .Where(t => contracts is null || contracts.Contains(t.notification.ScriptHash))
                .Where(t => eventNames is null || eventNames.Contains(t.notification.EventName));

            static (uint blockIndex, ushort txIndex, NotificationRecord notification) ParseNotification(byte[] key, byte[] value)
            {
                var blockIndex = BinaryPrimitives.ReadUInt32BigEndian(key.AsSpan(0, sizeof(uint)));
                var txIndex = BinaryPrimitives.ReadUInt16BigEndian(key.AsSpan(sizeof(uint), sizeof(ushort)));
                return (blockIndex, txIndex, notification: value.AsSerializable<NotificationRecord>());
            }
        }

        public async Task RunAsync(IConsole console, CancellationToken token)
        {
            if (node.IsRunning()) { throw new Exception("Node already running"); }

            var tcs = new TaskCompletionSource<bool>();
            _ = Task.Run(() =>
            {
                try
                {
                    var wallet = DevWallet.FromExpressWallet(node.Wallet, neoSystem.Settings.AddressVersion);
                    var defaultAccount = node.Wallet.Accounts.Single(a => a.IsDefault);

                    using var mutex = new Mutex(true, GLOBAL_PREFIX + defaultAccount.ScriptHash);

                    consolePlugin.Start(console);
                    webServerPlugin.Start();
                    neoSystem.StartNode(new Neo.Network.P2P.ChannelsConfig
                    {
                        Tcp = new IPEndPoint(IPAddress.Loopback, node.TcpPort),
                        WebSocket = new IPEndPoint(IPAddress.Loopback, node.WebSocketPort),
                    });
                    dbftPlugin.Start(wallet);

                    // DevTracker looks for a string that starts with "Neo express is running" to confirm that the instance has started
                    // Do not remove or re-word this console output:
                    consolePlugin.WriteLine($"Neo express is running ({expressStorage.Name})");

                    var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(token, shutdownTokenSource.Token);
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



        static Neo.Consensus.Settings GetConsensusSettings(IExpressChain chain)
        {
            var settings = new Dictionary<string, string>()
                {
                    { "PluginConfiguration:Network", $"{chain.Network}" },
                    { "PluginConfiguration:RecoveryLogs", CONSENSUS_STATE_STORE_NAME },
                    { "IgnoreRecoveryLogs", "true" }
                };

            var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            return new Neo.Consensus.Settings(config.GetSection("PluginConfiguration"));
        }
    }
}
