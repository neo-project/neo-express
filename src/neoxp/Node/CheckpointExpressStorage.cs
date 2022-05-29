using System;
using System.IO;
using Neo;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;
using Nito.Disposables;
using RocksDbSharp;

namespace NeoExpress.Node
{
    class CheckpointExpressStorage : IExpressStorage
    {
        readonly IDisposable disposable;

        public string Name => nameof(CheckpointExpressStorage);
        public IStore ChainStore { get; }
        public IStore AppLogsStore { get; }
        public IStore NotificationsStore { get; }

        private CheckpointExpressStorage()
        {
            disposable = NoopDisposable.Instance;
            ChainStore = new MemoryTrackingStore(NullStore.Instance);
            AppLogsStore = new MemoryTrackingStore(NullStore.Instance);
            NotificationsStore = new MemoryTrackingStore(NullStore.Instance);
        }

        private CheckpointExpressStorage(RocksDb db, string checkpointPath)
        {
            this.disposable = new AnonymousDisposable(() => 
            {
                db.Dispose();
                if (string.IsNullOrEmpty(checkpointPath) && Directory.Exists(checkpointPath))
                {
                    Directory.Delete(checkpointPath, recursive: true);
                }
            });

            ChainStore = new MemoryTrackingStore(
                CreateReadOnlyStore(db, db.GetDefaultColumnFamily()));
            AppLogsStore = new MemoryTrackingStore(
                GetReadOnlyStore(db, ExpressSystem.APP_LOGS_STORE_NAME));
            NotificationsStore = new MemoryTrackingStore(
                GetReadOnlyStore(db, ExpressSystem.NOTIFICATIONS_COLUMN_STORE_NAME));

            static IReadOnlyStore GetReadOnlyStore(RocksDb db, string name)
                => db.TryGetColumnFamily(name, out var columnFamily)
                    ? CreateReadOnlyStore(db, columnFamily)
                    : NullStore.Instance;

            static RocksDbStore CreateReadOnlyStore(RocksDb db, ColumnFamilyHandle columnFamily)
                => new RocksDbStore(db, columnFamily, readOnly: true, shared: true);
        }

        public static CheckpointExpressStorage OpenCheckpoint(string checkpointPath, uint? network = null, byte? addressVersion = null, UInt160? scriptHash = null)
        {
            var checkpointTempPath = RocksDbUtility.GetTempPath();
            var metadata = RocksDbUtility.RestoreCheckpoint(checkpointPath, checkpointTempPath, network, addressVersion, scriptHash);
            var db = RocksDbUtility.OpenReadOnlyDb(checkpointTempPath);
            return new CheckpointExpressStorage(db, checkpointTempPath);
        }

        public static IExpressStorage OpenForDiscard(string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                try
                {
                    var db = RocksDbUtility.OpenReadOnlyDb(path);
                    return new CheckpointExpressStorage(db, string.Empty);
                }
                catch {}
            }

            return new CheckpointExpressStorage();
        }

        public void Dispose() => disposable.Dispose();
    }
}
