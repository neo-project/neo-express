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

namespace NeoExpress.Neo3.Node
{
    internal class ExpressAppLogsPlugin : Plugin, IPersistencePlugin
    {
        private readonly IStore store;
        private const byte APP_LOGS_PREFIX = 0xf0;

        public ExpressAppLogsPlugin(IStore store)
        {
            this.store = store;
        }

        [RpcMethod]
        public JObject GetApplicationLog(JArray _params)
        {
            UInt256 hash = UInt256.Parse(_params[0].AsString());
            byte[] value = store.TryGet(APP_LOGS_PREFIX, hash.ToArray());
            if (value is null)
                throw new RpcException(-100, "Unknown transaction");
            return JObject.Parse(Encoding.UTF8.GetString(value));
        }

        public void OnPersist(StoreView _, IReadOnlyList<ApplicationExecuted> applicationExecutedList)
        {
            foreach (var appExec in applicationExecutedList)
            {
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
            }
        }
    }
}
