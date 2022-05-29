using System;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;
using RocksDbSharp;

namespace NeoExpress.Node
{
    class RocksDbExpressStorage : IExpressStorage
    {
        internal const string APP_LOGS_COLUMN_FAMILY_NAME = "app-logs-store";
        internal const string NOTIFICATIONS_COLUMN_FAMILY_NAME = "notifications-store";

        readonly RocksDb db;

        public string Name => nameof(RocksDbExpressStorage);
        public IStore Chain { get; }
        public IStore ConsensusState { get; }
        public IStore AppLogs { get; }
        public IStore Notifications { get; }

        public RocksDbExpressStorage(string path) : this(RocksDbUtility.OpenDb(path))
        {
        }

        public RocksDbExpressStorage(RocksDb db)
        {
            this.db = db;
            Chain = CreateStore(db, db.GetDefaultColumnFamily());
            ConsensusState = CreateStore(db, GetOrCreateColumnFamily(db, nameof(ConsensusState)));
            AppLogs = CreateStore(db, GetOrCreateColumnFamily(db, APP_LOGS_COLUMN_FAMILY_NAME));
            Notifications = CreateStore(db, GetOrCreateColumnFamily(db, NOTIFICATIONS_COLUMN_FAMILY_NAME));
            
            static IStore CreateStore(RocksDb db, ColumnFamilyHandle columnFamily)
                => new RocksDbStore(db, columnFamily, readOnly: false, shared: true);

            static ColumnFamilyHandle GetOrCreateColumnFamily(RocksDb db, string name) 
                => db.TryGetColumnFamily(name, out var columnFamily)
                    ? columnFamily
                    : db.CreateColumnFamily(new ColumnFamilyOptions(), name);
        }

        public void Dispose()
        {
            Notifications.Dispose();
            AppLogs.Dispose();
            Chain.Dispose();
            db.Dispose();
        }
    }
}
