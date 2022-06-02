using Neo;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;
using RocksDbSharp;

namespace NeoExpress.Node
{
    class RocksDbExpressStorage : IExpressStorage
    {
        readonly RocksDb db;

        public RocksDbExpressStorage(string path) : this(RocksDbUtility.OpenDb(path))
        {
        }

        public RocksDbExpressStorage(RocksDb db)
        {
            this.db = db;
        }

        public string Name => nameof(RocksDbExpressStorage);

        public void Dispose()
        {
            throw new System.NotImplementedException();
        }

        public IStore GetStore(string? path)
        {
            if (path is null)
                return new RocksDbStore(db, db.GetDefaultColumnFamily(), readOnly: false, shared: true);

            if (db.TryGetColumnFamily(path, out var columnFamily))
                return new RocksDbStore(db, columnFamily, readOnly: false, shared: true);

            columnFamily = db.CreateColumnFamily(new ColumnFamilyOptions(), path);
            return new RocksDbStore(db, columnFamily, readOnly: false, shared: true);
        }

        public void CreateCheckpoint(string checkPointFileName, uint network, byte addressVersion, UInt160 scriptHash)
            => RocksDbUtility.CreateCheckpoint(db, checkPointFileName, network, addressVersion, scriptHash);
    }
}
