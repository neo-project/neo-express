using System;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;
using RocksDbSharp;

namespace NeoExpress.Node
{
    class RocksDbExpressStorage : IExpressStorage
    {
        readonly RocksDb db;

        public string Name => nameof(RocksDbExpressStorage);
        public IStore ChainStore { get; }
        public IStore AppLogsStore { get; }
        public IStore NotificationsStore { get; }

        public RocksDbExpressStorage(string path) : this(RocksDbUtility.OpenDb(path))
        {
        }

        public RocksDbExpressStorage(RocksDb db)
        {
            this.db = db;
            ChainStore = CreateStore(db, db.GetDefaultColumnFamily());
            AppLogsStore = CreateStore(db, GetOrCreateColumnFamily(db, ExpressSystem.APP_LOGS_STORE_NAME));
            NotificationsStore = CreateStore(db, GetOrCreateColumnFamily(db, ExpressSystem.NOTIFICATIONS_COLUMN_STORE_NAME));
            
            static IStore CreateStore(RocksDb db, ColumnFamilyHandle columnFamily)
                => new RocksDbStore(db, columnFamily, readOnly: false, shared: true);

            static ColumnFamilyHandle GetOrCreateColumnFamily(RocksDb db, string name) 
                => db.TryGetColumnFamily(name, out var columnFamily)
                    ? columnFamily
                    : db.CreateColumnFamily(new ColumnFamilyOptions(), name);
        }

        public void Dispose()
        {
            NotificationsStore.Dispose();
            AppLogsStore.Dispose();
            ChainStore.Dispose();
            db.Dispose();
        }
    }
}
