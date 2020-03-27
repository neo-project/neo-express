using Neo.Persistence;
using Neo.Plugins;
using NeoExpress.Neo3.Persistence;

namespace NeoExpress.Neo3.Node
{
    class CheckpointStoragePlugin : Plugin, IStoragePlugin
    {
        private readonly string path;

        public CheckpointStoragePlugin(string path)
        {
            this.path = path;
        }

        public IStore GetStore() => new CheckpointStorage(path);
    }
}
