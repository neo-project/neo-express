using Neo.Persistence;
using Neo.Plugins;
using NeoExpress.Neo3.Persistence;

namespace NeoExpress.Neo3.Node
{
    class CheckpointStorePlugin : Plugin, IStoragePlugin
    {
        private readonly string path;

        public CheckpointStorePlugin(string path)
        {
            this.path = path;
        }

        public IStore GetStore() 
            => new CheckpointStore(RocksDbStore.OpenReadOnly(path));
    }
}
