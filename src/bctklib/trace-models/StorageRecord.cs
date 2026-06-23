// Copyright (C) 2015-2026 The Neo Project.
//
// StorageRecord.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using MessagePack;
using Neo.SmartContract;
using System.Buffers;

namespace Neo.BlockchainToolkit.TraceDebug
{
    [MessagePackObject]
    public class StorageRecord : ITraceDebugRecord
    {
        public const int RecordKey = 6;

        [Key(0)]
        public readonly UInt160 ScriptHash;

        [Key(1)]
        public readonly IReadOnlyDictionary<byte[], StorageItem> Storages;

        public StorageRecord(UInt160 scriptHash, IReadOnlyDictionary<byte[], StorageItem> storages)
        {
            ScriptHash = scriptHash;
            Storages = storages;
        }

        public static void Write(IBufferWriter<byte> writer, MessagePackSerializerOptions options, UInt160 scriptHash, IEnumerable<(StorageKey key, StorageItem item)> storages)
        {
            var mpWriter = new MessagePackWriter(writer);
            Write(ref mpWriter, options, scriptHash, storages);
            mpWriter.Flush();
        }

        public static void Write(ref MessagePackWriter writer, MessagePackSerializerOptions options, UInt160 scriptHash, IEnumerable<(StorageKey key, StorageItem item)> storages)
        {
            // Materialize once. storages is typically a lazy iterator returned by
            // DataCache.Find (a prefix scan over the snapshot); the previous .Count()
            // followed by foreach enumerated it twice, scanning the contract storage
            // twice for every VM instruction while tracing.
            var materialized = storages as IReadOnlyCollection<(StorageKey key, StorageItem item)>
                ?? storages.ToList();
            if (materialized.Count <= 0)
                return;

            var byteArrayFormatter = options.Resolver.GetFormatterWithVerify<byte[]>();
            var storageItemFormatter = options.Resolver.GetFormatterWithVerify<StorageItem>();

            writer.WriteArrayHeader(2);
            writer.WriteInt32(RecordKey);
            writer.WriteArrayHeader(2);
            options.Resolver.GetFormatterWithVerify<UInt160>().Serialize(ref writer, scriptHash, options);
            writer.WriteMapHeader(materialized.Count);
            foreach (var (key, item) in materialized)
            {
                writer.Write(key.Key.Span);
                storageItemFormatter.Serialize(ref writer, item, options);
            }
        }
    }
}
