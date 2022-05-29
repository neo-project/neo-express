using System;
using System.Linq;
using Neo.Persistence;
using Neo.Plugins;

namespace NeoExpress.Node
{
    partial class ExpressSystem
    {
        class StorageProviderPlugin : Plugin, IStorageProvider
        {
            readonly IExpressStorage expressStorage;

            public StorageProviderPlugin(IExpressStorage expressStorage)
            {
                this.expressStorage = expressStorage;
            }

            public IStore GetStore(string path)
                => string.IsNullOrEmpty(path)
                    ? expressStorage.Chain 
                    : path.Equals("ConsensusState")
                        ? expressStorage.ConsensusState 
                        : throw new ArgumentException($"Unknown ExpressStorage {path}", nameof(path));
        }
    }
}
