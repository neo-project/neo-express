using System.Linq;
using Neo.Persistence;
using Neo.Plugins;

namespace NeoExpress.Node
{
    public partial class ExpressSystem
    {
        class StorageProviderPlugin : Plugin, IStorageProvider
        {
            readonly IStorageProvider provider;

            public IStorageProvider Provider => provider;

            public StorageProviderPlugin(IStorageProvider provider)
            {
                this.provider = provider;
            }

            public IStore GetStore(string path) => provider.GetStore(path);

            static IStorageProvider GetStorageProvider()
            {
                return (IStorageProvider)Plugin.Plugins.Single(p => p is IStorageProvider);
            }
        }
    }
}
