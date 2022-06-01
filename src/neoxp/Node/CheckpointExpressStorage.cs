using System;
using System.Collections.Immutable;
using System.IO;
using Neo;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;
using RocksDbSharp;

namespace NeoExpress.Node
{
    class CheckpointExpressStorage : IExpressStorage
    {
        readonly RocksDb? db;
        readonly IDisposable disposable;
        readonly Lazy<IStore> defaultStore;
        ImmutableDictionary<string, IStore> stores = ImmutableDictionary<string, IStore>.Empty;
        public string Name => nameof(CheckpointExpressStorage);

        CheckpointExpressStorage()
        {
            disposable = Nito.Disposables.NoopDisposable.Instance;
            defaultStore = new Lazy<IStore>(() => new MemoryTrackingStore(GetUnderlyingStore(null)));
        }

        CheckpointExpressStorage(RocksDb db, string checkpointTempPath = "")
        {
            this.db = db;
            defaultStore = new Lazy<IStore>(() => new MemoryTrackingStore(GetUnderlyingStore(null)));

            this.disposable = string.IsNullOrEmpty(checkpointTempPath)
                ? Nito.Disposables.NoopDisposable.Instance
                : new Nito.Disposables.AnonymousDisposable(() => 
                {
                    if (Directory.Exists(checkpointTempPath))
                    {
                        Directory.Delete(checkpointTempPath, true);
                    }
                });
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
        

        public void Dispose()
        {
            db?.Dispose();
            disposable.Dispose();
        }

        public IStore GetStore(string? path)
        {
            if (path is null) return defaultStore.Value;
            return ImmutableInterlocked.GetOrAdd(ref stores, path,
                key => new MemoryTrackingStore(GetUnderlyingStore(key)));
        }

        IReadOnlyStore GetUnderlyingStore(string? path)
        {
            if (db is null) return NullStore.Instance;
            if (path is null) return CreateReadOnlyStore(db, db.GetDefaultColumnFamily());

            return db.TryGetColumnFamily(path, out var columnFamily)
                ? CreateReadOnlyStore(db, columnFamily)
                : NullStore.Instance;
            
            static RocksDbStore CreateReadOnlyStore(RocksDb db, ColumnFamilyHandle columnFamily)
                => new RocksDbStore(db, columnFamily, readOnly: true, shared: true);
        }
    }
}
