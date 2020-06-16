using System;
using System.Collections.Generic;
using System.Linq;
using Neo.Persistence;
using Neo.Plugins;
using Neo.Seattle.Persistence;

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

        class NullReadOnlyStore : IReadOnlyStore
        {
            IEnumerable<(byte[] Key, byte[] Value)> IReadOnlyStore.Find(byte table, byte[]? prefix) 
                => Enumerable.Empty<(byte[] Key, byte[] Value)>();

            byte[]? IReadOnlyStore.TryGet(byte table, byte[]? key) => null;
        }
    }
}
