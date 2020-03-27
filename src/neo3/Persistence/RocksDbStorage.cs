using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Neo.Persistence;
using RocksDbSharp;

namespace NeoExpress.Neo3.Persistence
{
    partial class RocksDbStorage : IStore
    {
        private readonly RocksDb db;
        private readonly ConcurrentDictionary<byte, ColumnFamilyHandle> columnFamilies = new ConcurrentDictionary<byte, ColumnFamilyHandle>();
        private readonly object @lock = new object();
        private readonly ReadOptions readOptions = new ReadOptions();
        private readonly WriteOptions writeOptions = new WriteOptions();

        private RocksDbStorage(string path, IEnumerable<string>? columnFamilyNames = null)
        {
            var options = new DbOptions()
                .SetCreateIfMissing(true);

            var columnFamilies = new ColumnFamilies();
            foreach (var name in columnFamilyNames ?? Enumerable.Empty<string>())
            {
                columnFamilies.Add(name, new ColumnFamilyOptions());
            }
            db = RocksDb.Open(options, path, columnFamilies);
        }

        public static RocksDbStorage Open(string path)
        {
            try
            {
                var columnFamilies = RocksDb.ListColumnFamilies(new DbOptions(), path);
                return new RocksDbStorage(path, columnFamilies);
            }
            catch (RocksDbException)
            {
                return new RocksDbStorage(path);
            }
        }

        public void Dispose()
        {
            db.Dispose();
        }

        public ISnapshot GetSnapshot() => new Snapshot(this);

        ColumnFamilyHandle GetColumnFamily(byte table)
        {
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

            if (columnFamilies.TryGetValue(table, out var columnFamily))
            {
                return columnFamily;
            }

            lock (@lock) 
            {
                columnFamily = GetColumnFamilyFromDatabase();
                columnFamilies.TryAdd(table, columnFamily);
                return columnFamily;
            }
        }

        public byte[] TryGet(byte table, byte[]? key)
        {
            return db.Get(key ?? Array.Empty<byte>(), GetColumnFamily(table), readOptions);
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Find(byte table, byte[] prefix)
        {
            using var iterator = db.NewIterator(GetColumnFamily(table), readOptions);
            for (iterator.Seek(prefix); iterator.Valid(); iterator.Next())
            {
                var key = iterator.Key();
                if (key.Length < prefix.Length) break;
                if (!key.AsSpan().StartsWith(prefix)) break;
                yield return (key, iterator.Value());
            }
        }
        
        public void Put(byte table, byte[]? key, byte[] value)
        {
            db.Put(key ?? Array.Empty<byte>(), value, GetColumnFamily(table), writeOptions);
        }

        public void Delete(byte table, byte[]? key)
        {
            db.Remove(key ?? Array.Empty<byte>(), GetColumnFamily(table), writeOptions);
        }
    }
}
