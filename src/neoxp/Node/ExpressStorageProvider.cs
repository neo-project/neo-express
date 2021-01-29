using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;

namespace NeoExpress.Node
{
    internal class ExpressStorageProvider : Plugin, IStorageProvider
    {
        public readonly IStore store;

        public ExpressStorageProvider(IStore store)
        {
            this.store = store;
        }

        public IStore GetStore() => store;
    }
}
