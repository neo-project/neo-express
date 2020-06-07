using Neo.Persistence;
using Neo.Plugins;
using NeoExpress.Neo3.Persistence;

namespace NeoExpress.Neo3.Node
{
    class RocksDbStoragePlugin : Plugin, IStoragePlugin
    {
        private readonly string path;

        public RocksDbStoragePlugin(string path)
        {
            this.path = path;
        }

        public IStore GetStore() => RocksDbStore.Open(path);
    }
}
