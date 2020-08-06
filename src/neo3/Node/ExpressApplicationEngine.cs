using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;

namespace NeoExpress.Neo3.Node
{
    internal class ExpressApplicationEngine : ApplicationEngine
    {
        public ExpressApplicationEngine(TriggerType trigger, IVerifiable container, StoreView snapshot, long gas, bool testMode = false) 
            : base(trigger, container, snapshot, gas, testMode)
        {
        }
    }
}
