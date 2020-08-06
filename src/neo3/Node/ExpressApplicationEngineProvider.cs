using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;

namespace NeoExpress.Neo3.Node
{
    internal class ExpressApplicationEngineProvider : Plugin, IApplicationEngineProvider
    {
        public ApplicationEngine? Create(TriggerType trigger, IVerifiable container, StoreView snapshot, long gas, bool testMode = false)
        {
            // eventually put logic to determine if we are tracing or not
            // returning null will use default ApplicationEngine
            return null;

            // var traceDebugSink = create debug sink
            // return new ExpressApplicationEngine(traceDebugSink, trigger, container, snapshot, gas, testMode);
        }
    }
}
