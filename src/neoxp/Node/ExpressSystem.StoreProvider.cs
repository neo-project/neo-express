using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;

namespace NeoExpress.Node
{
    partial class ExpressSystem
    {
        internal class StoreProvider : IStoreProvider
        {
            public readonly IExpressStorage expressStorage;

            public StoreProvider(IExpressStorage expressStorage)
            {
                this.expressStorage = expressStorage;
            }

            public string Name => nameof(StoreProvider);
            public IStore GetStore(string path) => expressStorage.GetStore(path);
        }
    }
}