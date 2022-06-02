using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;

namespace NeoExpress.Node
{
    internal class ExpressStoreProvider : IStoreProvider
    {
        public readonly IExpressStorage expressStorage;

        public ExpressStoreProvider(IExpressStorage expressStorage)
        {
            this.expressStorage = expressStorage;
        }

        public string Name => nameof(ExpressStoreProvider);
        public IStore GetStore(string path) => expressStorage.GetStore(path);
    }
}
