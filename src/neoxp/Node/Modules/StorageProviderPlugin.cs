using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;

namespace NeoExpress.Node
{
    internal class StorageProviderPlugin : Plugin, IStorageProvider
    {
        public readonly IExpressStorage expressStorage;

        public StorageProviderPlugin(IExpressStorage expressStorage)
        {
            this.expressStorage = expressStorage;
        }

        public IStore GetStore(string path) => expressStorage.GetStore(path);
    }
}
