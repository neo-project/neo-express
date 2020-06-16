using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using Neo.Persistence;
using RocksDbSharp;

namespace NeoExpress.Neo3.Persistence
{
    public partial class RocksDbStore : IStore
    {
        private readonly bool readOnly;
        private readonly RocksDb db;
        private readonly ConcurrentDictionary<byte, ColumnFamilyHandle> columnFamilyCache;
        private readonly ReadOptions readOptions = new ReadOptions();
        private readonly WriteOptions writeOptions = new WriteOptions();
        private readonly WriteOptions writeSyncOptions = new WriteOptions().SetSync(true);

        public static RocksDbStore Open(string path)
        {
            var columnFamilies = GetColumnFamilies(path);
            var db = RocksDb.Open(new DbOptions().SetCreateIfMissing(true), path, columnFamilies);
            return new RocksDbStore(db, columnFamilies);
        }

        public static RocksDbStore OpenReadOnly(string path)
        {
            var columnFamilies = GetColumnFamilies(path);
            var db = RocksDb.OpenReadOnly(new DbOptions(), path, columnFamilies, false);
            return new RocksDbStore(db, columnFamilies, true);
        }

        RocksDbStore(RocksDb db, ColumnFamilies columnFamilies, bool readOnly = false)
        {
            this.readOnly = readOnly;
            this.db = db;
            this.columnFamilyCache = new ConcurrentDictionary<byte, ColumnFamilyHandle>(EnumerateColumnFamlies(db, columnFamilies));

            static IEnumerable<KeyValuePair<byte, ColumnFamilyHandle>> EnumerateColumnFamlies(RocksDb db, ColumnFamilies columnFamilies)
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
        }

        private const string ADDRESS_FILENAME = "ADDRESS.neo-express";

        private static string GetAddressFilePath(string directory) =>
            Path.Combine(directory, ADDRESS_FILENAME);

        public void CreateCheckpoint(string checkPointFileName, long magic, string scriptHash)
        {
            if (File.Exists(checkPointFileName))
            {
                throw new ArgumentException("checkpoint file already exists", nameof(checkPointFileName));
            }

            string tempPath;
            do
            {
                tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            }
            while (Directory.Exists(tempPath));

            try
            {
                {
                    using var checkpoint = db.Checkpoint();
                    checkpoint.Save(tempPath);
                }

                {
                    using var stream = File.OpenWrite(GetAddressFilePath(tempPath));
                    using var writer = new StreamWriter(stream);
                    writer.WriteLine(magic);
                    writer.WriteLine(scriptHash);
                }

                ZipFile.CreateFromDirectory(tempPath, checkPointFileName);
            }
            finally
            {
                Directory.Delete(tempPath, true);
            }
        }

        public static void RestoreCheckpoint(string checkPointArchive, string restorePath, long magic, string scriptHash)
        {
            ZipFile.ExtractToDirectory(checkPointArchive, restorePath);
            var addressFile = ValidateCheckpoint(restorePath, magic, scriptHash);
            if (File.Exists(addressFile))
            {
                File.Delete(addressFile);
            }
        }

        static string ValidateCheckpoint(string checkPointDirectory, long magic, string scriptHash)
        {
            var addressFile = GetAddressFilePath(checkPointDirectory);
            if (!File.Exists(addressFile))
            {
                throw new Exception("Invalid Checkpoint");
            }

            using var stream = File.OpenRead(addressFile);
            using var reader = new StreamReader(stream);
            var checkPointMagic = long.Parse(reader.ReadLine() ?? string.Empty);
            var checkPointScriptHash = reader.ReadLine() ?? string.Empty;

            if (magic != checkPointMagic || scriptHash != checkPointScriptHash)
            {
                throw new Exception("Invalid Checkpoint");
            }
            
            return addressFile;
        }

        static ColumnFamilies GetColumnFamilies(string path)
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

        public void Dispose()
        {
            db.Dispose();
        }

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

        byte[]? IReadOnlyStore.TryGet(byte table, byte[]? key)
        {
            return db.Get(key ?? Array.Empty<byte>(), GetColumnFamily(table), readOptions);
        }

        IEnumerable<(byte[] Key, byte[] Value)> IReadOnlyStore.Find(byte table, byte[]? prefix)
        {
            return Find(db, prefix, GetColumnFamily(table), readOptions);
        }
        
        ISnapshot IStore.GetSnapshot() => readOnly 
            ? throw new InvalidOperationException() 
            : new Snapshot(this);

        void IStore.Put(byte table, byte[]? key, byte[] value)
        {
            if (readOnly) throw new InvalidOperationException();
            db.Put(key ?? Array.Empty<byte>(), value, GetColumnFamily(table), writeOptions);
        }

        void IStore.PutSync(byte table, byte[]? key, byte[] value)
        {
            if (readOnly) throw new InvalidOperationException();
            db.Put(key ?? Array.Empty<byte>(), value, GetColumnFamily(table), writeSyncOptions);
        }

        void IStore.Delete(byte table, byte[]? key)
        {
            if (readOnly) throw new InvalidOperationException();
            db.Remove(key ?? Array.Empty<byte>(), GetColumnFamily(table), writeOptions);
        }

        static IEnumerable<(byte[] key, byte[] value)> Find(RocksDb db, byte[]? prefix, ColumnFamilyHandle columnFamily, ReadOptions? readOptions = null)
        {
            prefix ??= Array.Empty<byte>();
            using var iterator = db.NewIterator(columnFamily, readOptions);
            for (iterator.Seek(prefix); iterator.Valid(); iterator.Next())
            {
                var key = iterator.Key();
                if (key.Length < prefix.Length) break;
                if (!key.AsSpan().StartsWith(prefix)) break;
                yield return (key, iterator.Value());
            }
        }
    }
}
