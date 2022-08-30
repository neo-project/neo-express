using Neo.Persistence;

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
