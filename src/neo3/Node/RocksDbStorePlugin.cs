using Neo.Persistence;
using Neo.Plugins;
using Neo.Seattle.Persistence;

namespace NeoExpress.Neo3.Node
{
    class RocksDbStorePlugin : Plugin, IStoragePlugin
    {
        private readonly string path;

        public RocksDbStorePlugin(string path)
        {
            this.path = path;
        }

        public IStore GetStore() => RocksDbStore.Open(path);
    }
}
