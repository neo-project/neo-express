using System.Collections.Generic;
using ApplicationExecuted = Neo.Ledger.Blockchain.ApplicationExecuted;
using Neo.Persistence;
using Neo.Plugins;
using Neo.IO.Json;
using System;
using System.Linq;
using Neo.VM;
using Neo.IO;
using System.Text;
using Neo;
using System.Buffers.Binary;
using Neo.Ledger;
using NeoExpress.Neo3.Models;

namespace NeoExpress.Neo3.Node
{
    internal partial class ExpressAppLogsPlugin : Plugin, IPersistencePlugin
    {
        private readonly IStore store;
        private const byte APP_LOGS_PREFIX = 0xf0;
        private const byte NOTIFICATIONS_PREFIX = 0xf1;

        public ExpressAppLogsPlugin(IStore store)
        {
            this.store = store;
        }

        public static JObject? TryGetAppLog(IReadOnlyStore store, UInt256 txHash)
        {
            var value = store.TryGet(APP_LOGS_PREFIX, txHash.ToArray());
            if (value != null && value.Length != 0)
            {
                return JObject.Parse(Encoding.UTF8.GetString(value));
            }
            return null;
        }

        public static IEnumerable<(uint blockIndex, ushort txIndex, NotificationRecord notification)> GetNotifications(IReadOnlyStore store)
        {
            return store.Seek(NOTIFICATIONS_PREFIX, null, Neo.IO.Caching.SeekDirection.Forward)
                .Select(t => 
                {
                     var (blockIndex, txIndex) = ParseNotificationKey(t.Key);
                    return (blockIndex, txIndex, t.Value.AsSerializable<NotificationRecord>());
                });

            static (uint, ushort) ParseNotificationKey(byte[] key)
            {
                var blockIndex = BinaryPrimitives.ReadUInt32BigEndian(key.AsSpan(0, sizeof(uint)));
                var txIndex = BinaryPrimitives.ReadUInt16BigEndian(key.AsSpan(sizeof(uint), sizeof(ushort)));
                return (blockIndex, txIndex);
            }
        }
        
        public void OnPersist(StoreView snapshot, IReadOnlyList<ApplicationExecuted> applicationExecutedList)
        {
            if (applicationExecutedList.Count > ushort.MaxValue)
            {
                throw new Exception("applicationExecutedList too big");
            }

            var notificationIndex = new byte[sizeof(uint) + (2 * sizeof(ushort))];
                BinaryPrimitives.WriteUInt32BigEndian(
                    notificationIndex.AsSpan(0, sizeof(uint)),
                    snapshot.PersistingBlock.Index);

            for (int i = 0; i < applicationExecutedList.Count; i++)
            {
                ApplicationExecuted? appExec = applicationExecutedList[i];
                if (appExec.Transaction == null)
                {
                    continue;
                }

                JObject json = new JObject();
                json["txid"] = appExec.Transaction.Hash.ToString();
                json["trigger"] = appExec.Trigger;
                json["vmstate"] = appExec.VMState;
                json["gasconsumed"] = appExec.GasConsumed.ToString();
                try
                {
                    json["stack"] = appExec.Stack.Select(q => q.ToJson()).ToArray();
                }
                catch (InvalidOperationException)
                {
                    json["stack"] = "error: recursive reference";
                }
                json["notifications"] = appExec.Notifications.Select(q =>
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

                store.Put(APP_LOGS_PREFIX, appExec.Transaction.Hash.ToArray(), Encoding.UTF8.GetBytes(json.ToString()));

                if (appExec.VMState != VMState.FAULT)
                {
                    if (appExec.Notifications.Length > ushort.MaxValue)
                    {
                        throw new Exception("appExec.Notifications too big");
                    }

                     BinaryPrimitives.WriteUInt16BigEndian(
                            notificationIndex.AsSpan(sizeof(uint), sizeof(ushort)),
                            (ushort)i);

                    for (int j = 0; j < appExec.Notifications.Length; j++)
                    {
                        BinaryPrimitives.WriteUInt16BigEndian(
                            notificationIndex.AsSpan(sizeof(uint) + sizeof(ushort), sizeof(ushort)),
                            (ushort)j);
                        var record = new NotificationRecord(appExec.Notifications[j]);
                        store.Put(
                            NOTIFICATIONS_PREFIX,
                            notificationIndex.ToArray(),
                            record.ToArray());
                    }
                }
            }
        }
    }
}
