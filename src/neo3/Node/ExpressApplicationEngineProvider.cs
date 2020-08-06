using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;

namespace NeoExpress.Neo3.Node
{
    internal class ExpressApplicationEngineProvider : Plugin, IApplicationEngineProvider
    {
        public ApplicationEngine Create(TriggerType trigger, IVerifiable container, StoreView snapshot, long gas, bool testMode = false)
        {
            return new ExpressApplicationEngine(trigger, container, snapshot, gas, testMode);
        }
    }
}
