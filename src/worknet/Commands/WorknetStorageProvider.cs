using Neo.Persistence;

namespace NeoWorkNet.Commands;

class WorknetStorageProvider : IStoreProvider
{
    readonly IStore store;
    readonly Lazy<IStore> consensusStateStore = new(() => new MemoryStore());

    public WorknetStorageProvider(IStore store)
    {
        this.store = store;
    }

    public string Name => nameof(WorknetStorageProvider);

    public IStore GetStore(string path)
    {
        if (string.IsNullOrEmpty(path)) return store;
        if (path == "ConsensusState") return consensusStateStore.Value;
        throw new NotSupportedException();
    }
}
