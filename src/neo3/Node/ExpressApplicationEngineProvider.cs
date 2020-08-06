using System;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;

namespace NeoExpress.Neo3.Node
{
    using SysIO = System.IO;

    internal class ExpressApplicationEngineProvider : Plugin, IApplicationEngineProvider
    {
        public ApplicationEngine? Create(TriggerType trigger, IVerifiable container, StoreView snapshot, long gas, bool testMode = false)
        {
            if (trigger == TriggerType.Application
                && container is Transaction tx)
            {
                var path = SysIO.Path.Combine(Environment.CurrentDirectory, $"{tx.Hash}.neo-trace");
                var sink = new TraceDebugSink(SysIO.File.OpenWrite(path));
                return new ExpressApplicationEngine(sink, trigger, container, snapshot, gas, testMode);
            }

            return null;
        }
    }
}
