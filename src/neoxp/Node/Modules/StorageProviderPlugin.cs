using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;

namespace NeoExpress.Node
{
    internal class StorageProviderPlugin : Plugin, IStorageProvider
    {
        public readonly IStorageProvider storageProvider;

        public StorageProviderPlugin(IStorageProvider storageProvider)
        {
            this.storageProvider = storageProvider;
        }

        public IStore GetStore(string path) => storageProvider.GetStore(path);
    }
}
