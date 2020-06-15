using System;
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

        IReadOnlyStore GetReadOnlyStore()
        {
            try
            {
                return RocksDbStore.OpenReadOnly(path);
            }            
            catch (Exception)
            {
                return new NullReadOnlyStore();
            }
        }

        public IStore GetStore()
            => new CheckpointStore(GetReadOnlyStore());
    }
}
