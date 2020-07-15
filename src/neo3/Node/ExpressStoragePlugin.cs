using Neo.Persistence;
using Neo.Plugins;

namespace NeoExpress.Neo3.Node
{
    class ExpressStorageProvider : Plugin, IStorageProvider
    {
        public readonly IStore store;

        public ExpressStorageProvider(IStore store)
        {
            this.store = store;
        }

        public IStore GetStore() => store;
    }
}
