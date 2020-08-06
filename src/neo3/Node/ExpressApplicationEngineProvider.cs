using System;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;

namespace NeoExpress.Neo3.Node
{
    using SysPath = System.IO.Path;

    internal class ExpressApplicationEngineProvider : Plugin, IApplicationEngineProvider
    {
        public ApplicationEngine? Create(TriggerType trigger, IVerifiable container, StoreView snapshot, long gas, bool testMode = false)
        {
            if (trigger == TriggerType.Application
                && container is Transaction tx)
            {
                var name = $"{tx.Hash}.neo-trace.json";
                var path = SysPath.Combine(Environment.CurrentDirectory, name);
                var sink = new TraceDebugJsonSink(path);
                return new ExpressApplicationEngine(sink, trigger, container, snapshot, gas, testMode);
            }

            return null;
        }
    }
}
