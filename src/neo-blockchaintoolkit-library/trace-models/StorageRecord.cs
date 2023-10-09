// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using MessagePack;
using Neo.SmartContract;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

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
            var count = storages.Count();
            if (count <= 0)
                return;

            var byteArrayFormatter = options.Resolver.GetFormatterWithVerify<byte[]>();
            var storageItemFormatter = options.Resolver.GetFormatterWithVerify<StorageItem>();

            writer.WriteArrayHeader(2);
            writer.WriteInt32(RecordKey);
            writer.WriteArrayHeader(2);
            options.Resolver.GetFormatterWithVerify<UInt160>().Serialize(ref writer, scriptHash, options);
            writer.WriteMapHeader(count);
            foreach (var (key, item) in storages)
            {
                writer.Write(key.Key.Span);
                storageItemFormatter.Serialize(ref writer, item, options);
            }
        }
    }
}
