using System;
using System.Linq;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;
using Neo.Plugins;

namespace NeoExpress.Node
{
    partial class ExpressSystem
    {
        class StorageProviderPlugin : Plugin, IStorageProvider
        {
            readonly IExpressStorage expressStorage;
            readonly IStore consensusStateStore;

            public StorageProviderPlugin(IExpressStorage expressStorage)
            {
                this.expressStorage = expressStorage;
                // Express DBFTPlugin configured to ignore recovery logs, so there's no need
                // to save consensus state to disk
                this.consensusStateStore = new MemoryTrackingStore(NullStore.Instance);
            }

            public IStore GetStore(string path)
                => string.IsNullOrEmpty(path)
                    ? expressStorage.ChainStore 
                    : path.Equals(ExpressSystem.CONSENSUS_STATE_STORE_NAME)
                        ? consensusStateStore 
                        : throw new ArgumentException($"Unknown ExpressStorage {path}", nameof(path));
        }
    }
}
