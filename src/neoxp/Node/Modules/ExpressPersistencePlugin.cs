// Copyright (C) 2015-2024 The Neo Project.
//
// ExpressPersistencePlugin.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.IO;
using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.VM;
using NeoExpress.Models;
using System.Buffers.Binary;
using ApplicationExecuted = Neo.Ledger.Blockchain.ApplicationExecuted;

namespace NeoExpress.Node
{
    class ExpressPersistencePlugin : Plugin
    {
        const string APP_LOGS_STORE_PATH = "app-logs-store";
        const string NOTIFICATIONS_STORE_PATH = "notifications-store";

        IStore? appLogsStore;
        IStore? notificationsStore;
        ISnapshot? appLogsSnapshot;
        ISnapshot? notificationsSnapshot;

        public ExpressPersistencePlugin()
        {
            Blockchain.Committing += OnCommitting;
            Blockchain.Committed += OnCommitted;
        }

        public override void Dispose()
        {
            Blockchain.Committing -= OnCommitting;
            Blockchain.Committed -= OnCommitted;
            appLogsSnapshot?.Dispose();
            appLogsStore?.Dispose();
            notificationsSnapshot?.Dispose();
            notificationsStore?.Dispose();
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (this.appLogsStore is not null)
                throw new Exception($"{nameof(OnSystemLoaded)} already called");
            if (this.notificationsStore is not null)
                throw new Exception($"{nameof(OnSystemLoaded)} already called");

            appLogsStore = system.LoadStore(APP_LOGS_STORE_PATH);
            notificationsStore = system.LoadStore(NOTIFICATIONS_STORE_PATH);

            base.OnSystemLoaded(system);
        }

        public JObject? GetAppLog(UInt256 hash)
        {
            if (appLogsStore is null)
                throw new NullReferenceException(nameof(appLogsStore));
            var value = appLogsStore.TryGet(hash.ToArray());
            return value is not null && value.Length != 0
                ? JToken.Parse(Neo.Utility.StrictUTF8.GetString(value)) as JObject
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
            SeekDirection direction,
            IReadOnlySet<UInt160>? contracts,
            string eventName) => string.IsNullOrEmpty(eventName)
                ? GetNotifications(direction, contracts)
                : GetNotifications(direction, contracts,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase) { eventName });

        public IEnumerable<(uint blockIndex, ushort txIndex, NotificationRecord notification)> GetNotifications(
            SeekDirection direction = SeekDirection.Forward,
            IReadOnlySet<UInt160>? contracts = null,
            IReadOnlySet<string>? eventNames = null)
        {
            if (notificationsStore is null)
                throw new NullReferenceException(nameof(notificationsStore));

            var prefix = direction == SeekDirection.Forward
                ? Array.Empty<byte>()
                : backwardsNotificationsPrefix.Value;

            return notificationsStore.Seek(prefix, direction)
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

        void OnCommitting(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<ApplicationExecuted> applicationExecutedList)
        {
            if (appLogsStore is null)
                throw new NullReferenceException(nameof(appLogsStore));
            if (notificationsStore is null)
                throw new NullReferenceException(nameof(notificationsStore));

            appLogsSnapshot?.Dispose();
            notificationsSnapshot?.Dispose();
            appLogsSnapshot = appLogsStore.GetSnapshot();
            notificationsSnapshot = notificationsStore.GetSnapshot();

            if (applicationExecutedList.Count > ushort.MaxValue)
                throw new Exception("applicationExecutedList too big");

            var notificationIndex = new byte[sizeof(uint) + (2 * sizeof(ushort))];
            BinaryPrimitives.WriteUInt32BigEndian(
                notificationIndex.AsSpan(0, sizeof(uint)),
                block.Index);

            for (int i = 0; i < applicationExecutedList.Count; i++)
            {
                ApplicationExecuted appExec = applicationExecutedList[i];
                if (appExec.Transaction is null)
                    continue;

                var txJson = TxLogToJson(appExec);
                appLogsSnapshot.Put(appExec.Transaction.Hash.ToArray(), Neo.Utility.StrictUTF8.GetBytes(txJson.ToString()));

                if (appExec.VMState != VMState.FAULT)
                {
                    if (appExec.Notifications.Length > ushort.MaxValue)
                        throw new Exception("appExec.Notifications too big");

                    BinaryPrimitives.WriteUInt16BigEndian(notificationIndex.AsSpan(sizeof(uint), sizeof(ushort)), (ushort)i);

                    for (int j = 0; j < appExec.Notifications.Length; j++)
                    {
                        BinaryPrimitives.WriteUInt16BigEndian(
                            notificationIndex.AsSpan(sizeof(uint) + sizeof(ushort), sizeof(ushort)),
                            (ushort)j);
                        var record = new NotificationRecord(appExec.Notifications[j]);
                        notificationsSnapshot.Put(notificationIndex.ToArray(), record.ToArray());
                    }
                }
            }

            var blockJson = BlockLogToJson(block, applicationExecutedList);
            if (blockJson is not null)
            {
                appLogsSnapshot.Put(block.Hash.ToArray(), Neo.Utility.StrictUTF8.GetBytes(blockJson.ToString()));
            }
        }

        void OnCommitted(NeoSystem system, Block block)
        {
            appLogsSnapshot?.Commit();
            notificationsSnapshot?.Commit();
        }

        // TxLogToJson and BlockLogToJson copied from Neo.Plugins.LogReader in the ApplicationLogs plugin
        // to avoid dependency on LevelDBStore package

        static JObject TxLogToJson(ApplicationExecuted appExec)
        {
            global::System.Diagnostics.Debug.Assert(appExec.Transaction is not null);

            var txJson = new JObject();
            txJson["txid"] = appExec.Transaction.Hash.ToString();
            JObject trigger = new JObject();
            trigger["trigger"] = appExec.Trigger;
            trigger["vmstate"] = appExec.VMState;
            trigger["exception"] = GetExceptionMessage(appExec.Exception);
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

            static string? GetExceptionMessage(Exception exception)
            {
                return exception?.GetBaseException().Message;
            }
        }

        static JObject? BlockLogToJson(Block block, IReadOnlyList<ApplicationExecuted> applicationExecutedList)
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
