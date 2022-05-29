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
        public IStore Chain { get; }
        public IStore AppLogs { get; }
        public IStore ConsensusState { get; }
        public IStore Notifications { get; }

        private CheckpointExpressStorage()
        {
            disposable = NoopDisposable.Instance;
            Chain = new MemoryTrackingStore(NullStore.Instance);
            AppLogs = new MemoryTrackingStore(NullStore.Instance);
            ConsensusState = new MemoryTrackingStore(NullStore.Instance);
            Notifications = new MemoryTrackingStore(NullStore.Instance);
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

            Chain = new MemoryTrackingStore(
                CreateReadOnlyStore(db, db.GetDefaultColumnFamily()));
            AppLogs = new MemoryTrackingStore(
                GetReadOnlyStore(db, RocksDbExpressStorage.APP_LOGS_COLUMN_FAMILY_NAME));
            ConsensusState = new MemoryTrackingStore(
                GetReadOnlyStore(db, nameof(ConsensusState)));
            Notifications = new MemoryTrackingStore(
                GetReadOnlyStore(db, RocksDbExpressStorage.NOTIFICATIONS_COLUMN_FAMILY_NAME));

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
