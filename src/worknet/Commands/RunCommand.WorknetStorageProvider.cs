using Neo.Persistence;

namespace NeoWorkNet.Commands;

partial class RunCommand
{
    class WorknetStorageProvider : IStoreProvider
    {
        readonly IStore store;

        public WorknetStorageProvider(IStore store)
        {
            this.store = store;
        }

        public string Name => nameof(WorknetStorageProvider);

        public IStore GetStore(string path)
        {
            if (string.IsNullOrEmpty(path)) return store;
            if (path == "ConsensusState") return new MemoryStore();
            throw new NotSupportedException();
        }
    }
}
