// Copyright (C) 2015-2024 The Neo Project.
//
// ToolkitPersistencePlugin.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BlockchainToolkit.Persistence;
using Neo.IO;
using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.VM;
using RocksDbSharp;
using System.Buffers.Binary;
using System.Diagnostics;
using ApplicationExecuted = Neo.Ledger.Blockchain.ApplicationExecuted;

namespace Neo.BlockchainToolkit.Plugins
{
    public sealed class ToolkitPersistencePlugin : Plugin, INotificationsProvider
    {
        const string APP_LOGS_FAMILY_NAME = $".app-logs";
        const string NOTIFICATIONS_FAMILY_NAME = $".notifications";

        readonly RocksDb db;
        readonly ColumnFamilyHandle appLogsFamily;
        readonly ColumnFamilyHandle notificationsFamily;
        WriteBatch? writeBatch = null;
        bool disposed = false;

        public ToolkitPersistencePlugin(RocksDb db, string familyNamePrefix = nameof(ToolkitPersistencePlugin))
        {
            Blockchain.Committing += OnCommitting;
            Blockchain.Committed += OnCommitted;
            this.db = db;
            appLogsFamily = db.GetOrCreateColumnFamily(familyNamePrefix + APP_LOGS_FAMILY_NAME);
            notificationsFamily = db.GetOrCreateColumnFamily(familyNamePrefix + NOTIFICATIONS_FAMILY_NAME);
        }

        public override void Dispose()
        {
            if (!disposed)
            {
                Blockchain.Committing -= OnCommitting;
                Blockchain.Committed -= OnCommitted;
                writeBatch?.Dispose();
                disposed = true;
            }
        }

        public JObject? GetAppLog(UInt256 hash)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ToolkitPersistencePlugin));

            var value = db.Get(hash.ToArray(), appLogsFamily);
            return value is not null && value.Length != 0
                ? JToken.Parse(Neo.Utility.StrictUTF8.GetString(value)) as JObject
                : null;
        }

        public IEnumerable<NotificationInfo> GetNotifications(
            SeekDirection direction = SeekDirection.Forward,
            IReadOnlySet<UInt160>? contracts = null,
            IReadOnlySet<string>? eventNames = null)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ToolkitPersistencePlugin));

            var forward = direction == SeekDirection.Forward;
            var iterator = db.NewIterator(notificationsFamily);

            _ = forward ? iterator.SeekToFirst() : iterator.SeekToLast();
            while (iterator.Valid())
            {
                var info = ParseNotification(iterator.GetKeySpan(), iterator.Value());
                if (contracts?.Contains(info.Notification.ScriptHash) ?? false)
                    continue;
                if (eventNames?.Contains(info.Notification.EventName) ?? false)
                    continue;
                yield return info;
                _ = forward ? iterator.Next() : iterator.Prev();
            }

            static NotificationInfo ParseNotification(ReadOnlySpan<byte> key, byte[] value)
            {
                var blockIndex = BinaryPrimitives.ReadUInt32BigEndian(key.Slice(0, sizeof(uint)));
                var txIndex = BinaryPrimitives.ReadUInt16BigEndian(key.Slice(sizeof(uint), sizeof(ushort)));
                var notIndex = BinaryPrimitives.ReadUInt16BigEndian(key.Slice(sizeof(uint) + sizeof(ushort), sizeof(ushort)));
                return new NotificationInfo(blockIndex, txIndex, notIndex, value.AsSerializable<NotificationRecord>());
            }
        }

        void OnCommitting(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<ApplicationExecuted> executions)
        {
            if (disposed)
                return;

            writeBatch?.Dispose();
            writeBatch = new RocksDbSharp.WriteBatch();

            if (executions.Count > ushort.MaxValue)
                throw new Exception("ApplicationExecuted List too big");

            var notificationIndex = new byte[sizeof(uint) + (2 * sizeof(ushort))];
            BinaryPrimitives.WriteUInt32BigEndian(
                notificationIndex.AsSpan(0, sizeof(uint)),
                block.Index);

            for (int i = 0; i < executions.Count; i++)
            {
                ApplicationExecuted appExec = executions[i];
                if (appExec.Transaction is null)
                    continue;

                var txJson = TxLogToJson(appExec);
                writeBatch.Put(
                    appExec.Transaction.Hash.ToArray(),
                    Neo.Utility.StrictUTF8.GetBytes(txJson.ToString()),
                    appLogsFamily);

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
                        writeBatch.Put(notificationIndex, record.ToArray(), notificationsFamily);
                    }
                }
            }

            var blockJson = BlockLogToJson(block, executions);
            if (blockJson is not null)
            {
                writeBatch.Put(
                    block.Hash.ToArray(),
                    Neo.Utility.StrictUTF8.GetBytes(blockJson.ToString()),
                    appLogsFamily);
            }
        }

        void OnCommitted(NeoSystem system, Block block)
        {
            if (disposed)
                return;

            if (writeBatch is not null)
            {
                db.Write(writeBatch);
                writeBatch.Dispose();
                writeBatch = null;
            }
        }

        // TxLogToJson and BlockLogToJson copied from Neo.Plugins.LogReader in the ApplicationLogs plugin
        // to avoid dependency on LevelDBStore package

        static JObject TxLogToJson(ApplicationExecuted appExec)
        {
            Debug.Assert(appExec.Transaction is not null);

            JObject execution = ApplicationExecutedToJson(appExec);
            return new JObject()
            {
                ["txid"] = $"{appExec.Transaction.Hash}",
                ["executions"] = new JArray(execution)
            };
        }

        static JObject? BlockLogToJson(Block block, IReadOnlyList<ApplicationExecuted> executions)
        {
            var executionsJson = new JArray();
            foreach (var execution in executions)
            {
                if (execution.Transaction is null)
                    continue;
                executionsJson.Add(ApplicationExecutedToJson(execution));
            }
            if (executionsJson.Count == 0)
                return null;

            return new JObject()
            {
                ["blockhash"] = $"{block.Hash}",
                ["executions"] = executionsJson
            };
        }

        static JObject ApplicationExecutedToJson(ApplicationExecuted appExec)
        {
            return new JObject()
            {
                ["trigger"] = appExec.Trigger,
                ["vmstate"] = appExec.VMState,
                ["exception"] = appExec.Exception?.GetBaseException().Message,
                ["gasconsumed"] = appExec.GasConsumed.ToString(),
                ["stack"] = StackItemsToJson(appExec.Stack),
                ["notifications"] = new JArray(appExec.Notifications
                    .Select(n => new JObject()
                    {
                        ["contract"] = n.ScriptHash.ToString(),
                        ["eventname"] = n.EventName,
                        ["state"] = ArrayToJson(n.State),
                    }))
            };

            static JToken StackItemsToJson(VM.Types.StackItem[] items)
            {
                try
                {
                    return new JArray(items.Select(i => i.ToJson()));
                }
                catch (InvalidOperationException)
                {
                    return new JString("error: recursive reference");
                }
            }

            static JToken ArrayToJson(VM.Types.Array array)
            {
                try
                {
                    return array.ToJson();
                }
                catch (InvalidOperationException)
                {
                    return new JString("error: recursive reference");
                }
            }
        }
    }
}
