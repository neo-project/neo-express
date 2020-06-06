using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Neo.Persistence;
using RocksDbSharp;

namespace NeoExpress.Neo3.Persistence
{
    partial class RocksDbStore : IStore
    {
        private readonly RocksDb db;
        private readonly ConcurrentDictionary<byte, ColumnFamilyHandle> columnFamilyCache;
        private readonly ReadOptions readOptions = new ReadOptions();
        private readonly WriteOptions writeOptions = new WriteOptions();
        private readonly WriteOptions writeSyncOptions = new WriteOptions().SetSync(true);

        public RocksDbStore(string path)
        {
            var options = new DbOptions().SetCreateIfMissing(true);
            var columnFamilies = GetColumnFamilies(path);
            db = RocksDb.Open(options, path, columnFamilies);
            columnFamilyCache = new ConcurrentDictionary<byte, ColumnFamilyHandle>(GetColumnFamilyCache(db, columnFamilies));
        }

        internal static ColumnFamilies GetColumnFamilies(string path)
        {
            try
            {
                var names = RocksDb.ListColumnFamilies(new DbOptions(), path);
                var columnFamilyOptions = new ColumnFamilyOptions();
                var families = new ColumnFamilies();
                foreach (var name in names)
                {
                    families.Add(name, columnFamilyOptions);
                }
                return families;
            }
            catch (RocksDbException)
            {
                return new ColumnFamilies();
            }
        }

        internal static IEnumerable<KeyValuePair<byte, ColumnFamilyHandle>> GetColumnFamilyCache(RocksDb db, ColumnFamilies columnFamilies)
        {
            foreach (var descriptor in columnFamilies)
            {
                var name = descriptor.Name;
                if (byte.TryParse(descriptor.Name, out var key))
                {
                    var value = db.GetColumnFamily(name);
                    yield return KeyValuePair.Create(key, value);
                }
            }
        }

        public void Dispose()
        {
            db.Dispose();
        }

        public ISnapshot GetSnapshot() => new Snapshot(this);

        ColumnFamilyHandle GetColumnFamily(byte table)
        {
            if (columnFamilyCache.TryGetValue(table, out var columnFamily))
            {
                return columnFamily;
            }

            lock (columnFamilyCache) 
            {
                columnFamily = GetColumnFamilyFromDatabase();
                columnFamilyCache.TryAdd(table, columnFamily);
                return columnFamily;
            }

            ColumnFamilyHandle GetColumnFamilyFromDatabase()
            {
                var familyName = table.ToString();
                try
                {
                    return db.GetColumnFamily(familyName);
                }
                catch (KeyNotFoundException)
                {
                    return db.CreateColumnFamily(new ColumnFamilyOptions(), familyName);
                }
            }
        }

        public byte[]? TryGet(byte table, byte[]? key)
        {
            return db.Get(key ?? Array.Empty<byte>(), GetColumnFamily(table), readOptions);
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Find(byte table, byte[]? prefix)
        {
            return db.Find(prefix, GetColumnFamily(table), readOptions);
        }
        
        public void Put(byte table, byte[]? key, byte[] value)
        {
            db.Put(key ?? Array.Empty<byte>(), value, GetColumnFamily(table), writeOptions);
        }

        public void PutSync(byte table, byte[]? key, byte[] value)
        {
            db.Put(key ?? Array.Empty<byte>(), value, GetColumnFamily(table), writeSyncOptions);
        }

        public void Delete(byte table, byte[]? key)
        {
            db.Remove(key ?? Array.Empty<byte>(), GetColumnFamily(table), writeOptions);
        }
    }
}
