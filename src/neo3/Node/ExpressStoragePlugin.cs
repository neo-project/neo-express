using Neo.Persistence;
using Neo.Plugins;

namespace NeoExpress.Neo3.Node
{
    class ExpressStoragePlugin : Plugin, IStoragePlugin
    {
        public readonly IStore store;

        public ExpressStoragePlugin(IStore store)
        {
            this.store = store;
        }

        public IStore GetStore() => store;
    }
}
