// Copyright (C) 2015-2026 The Neo Project.
//
// TraceDebugTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using MessagePack;
using MessagePack.Resolvers;
using Neo;
using Neo.BlockchainToolkit.TraceDebug;
using Neo.Extensions;
using Neo.IO;
using Neo.SmartContract;
using Neo.VM;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace test.bctklib
{
    public class TraceDebugTests
    {
        MessagePackSerializerOptions options = MessagePackSerializerOptions.Standard.WithResolver(TraceDebugResolver.Instance);

        [Fact]
        public void can_deserialize_trace_record_with_no_gas()
        {
            var writer = new ArrayBufferWriter<byte>();
            TraceRecord_WriteWithoutGas(writer, options, VMState.BREAK, Array.Empty<Neo.VM.ExecutionContext>(), _ => UInt160.Zero);

            var record = MessagePackSerializer.Deserialize<ITraceDebugRecord>(writer.WrittenMemory, options, TestContext.Current.CancellationToken);

            Assert.IsType<TraceRecord>(record);
            if (record is TraceRecord traceRecord)
            {
                Assert.Equal(VMState.BREAK, traceRecord.State);
                Assert.Equal(0, traceRecord.GasConsumed);
            }
        }

        static void TraceRecord_WriteWithoutGas(
           IBufferWriter<byte> writer, MessagePackSerializerOptions options, VMState vmState,
           IReadOnlyCollection<Neo.VM.ExecutionContext> contexts, Func<Neo.VM.ExecutionContext, UInt160> getScriptIdentifier)
        {
            var mpWriter = new MessagePackWriter(writer);
            mpWriter.WriteArrayHeader(2);
            mpWriter.WriteInt32(TraceRecord.RecordKey);
            mpWriter.WriteArrayHeader(2);
            options.Resolver.GetFormatterWithVerify<VMState>().Serialize(ref mpWriter, vmState, options);
            mpWriter.WriteArrayHeader(contexts.Count);
            foreach (var context in contexts)
            {
                TraceRecord.StackFrame.Write(ref mpWriter, options, context, getScriptIdentifier(context));
            }
            mpWriter.Flush();
        }

        [Fact]
        public void can_deserialize_trace_record_with_gas()
        {
            var writer = new ArrayBufferWriter<byte>();
            TraceRecord.Write(writer, options, VMState.BREAK, 1000, Array.Empty<Neo.VM.ExecutionContext>(), _ => UInt160.Zero);

            var record = MessagePackSerializer.Deserialize<ITraceDebugRecord>(writer.WrittenMemory, options, TestContext.Current.CancellationToken);

            Assert.IsType<TraceRecord>(record);
            if (record is TraceRecord traceRecord)
            {
                Assert.Equal(VMState.BREAK, traceRecord.State);
                Assert.Equal(1000, traceRecord.GasConsumed);
            }
        }

        [Fact]
        public void can_deserialize_storage_record_with_array_header()
        {
            var scriptHash = UInt160.Parse("0001020304050607080900010203040506070809");
            var storages = new[] {
                (key: new StorageKey { Key = Convert.FromHexString("01")}, value: new StorageItem(Convert.FromHexString("11121314"))),
                (key: new StorageKey { Key = Convert.FromHexString("02")}, value: new StorageItem(Convert.FromHexString("21222324")))
            };

            var writer = new ArrayBufferWriter<byte>();
            StorageRecord_WriteWithStorageItemArrayHeader(writer, options, scriptHash, storages);

            var record = MessagePackSerializer.Deserialize<ITraceDebugRecord>(writer.WrittenMemory, options, TestContext.Current.CancellationToken);

            Assert.IsType<StorageRecord>(record);
            if (record is StorageRecord storageRecord)
            {
                Assert.Equal(scriptHash, storageRecord.ScriptHash);
                Assert.Equal(storages.Length, storageRecord.Storages.Count);
            }
        }

        [Fact]
        public void can_deserialize_storage_record_without_array_header()
        {
            var scriptHash = UInt160.Parse("0001020304050607080900010203040506070809");
            var storages = new[] {
                (key: new StorageKey { Key = Convert.FromHexString("01")}, value: new StorageItem(Convert.FromHexString("11121314"))),
                (key: new StorageKey { Key = Convert.FromHexString("02")}, value: new StorageItem(Convert.FromHexString("21222324")))
            };

            var writer = new ArrayBufferWriter<byte>();
            StorageRecord.Write(writer, options, scriptHash, storages);

            var record = MessagePackSerializer.Deserialize<ITraceDebugRecord>(writer.WrittenMemory, options, TestContext.Current.CancellationToken);

            Assert.IsType<StorageRecord>(record);
            if (record is StorageRecord storageRecord)
            {
                Assert.Equal(scriptHash, storageRecord.ScriptHash);
                Assert.Equal(storages.Length, storageRecord.Storages.Count);
            }
        }
        [Fact]
        public void storage_record_write_enumerates_lazy_storages_once()
        {
            var scriptHash = UInt160.Parse("0001020304050607080900010203040506070809");
            var source = new[] {
                (key: new StorageKey { Key = Convert.FromHexString("01")}, value: new StorageItem(Convert.FromHexString("11121314"))),
                (key: new StorageKey { Key = Convert.FromHexString("02")}, value: new StorageItem(Convert.FromHexString("21222324")))
            };

            var enumerateCount = 0;
            IEnumerable<(StorageKey key, StorageItem item)> Lazy()
            {
                foreach (var entry in source)
                {
                    enumerateCount++;
                    yield return entry;
                }
            }

            var lazyWriter = new ArrayBufferWriter<byte>();
            StorageRecord.Write(lazyWriter, options, scriptHash, Lazy());

            // A lazy DataCache.Find iterator must be walked exactly once, not once
            // for a .Count() and again for the write loop.
            Assert.Equal(source.Length, enumerateCount);

            // Materializing the iterator must not change the serialized bytes: writing
            // the same entries from an already-materialized array produces an identical
            // record, so the optimization is purely a performance change.
            var arrayWriter = new ArrayBufferWriter<byte>();
            StorageRecord.Write(arrayWriter, options, scriptHash, source);
            Assert.Equal(arrayWriter.WrittenMemory.ToArray(), lazyWriter.WrittenMemory.ToArray());

            // And the record still round-trips through deserialization.
            var record = MessagePackSerializer.Deserialize<ITraceDebugRecord>(lazyWriter.WrittenMemory, options, TestContext.Current.CancellationToken);
            Assert.IsType<StorageRecord>(record);
            var storageRecord = (StorageRecord)record;
            Assert.Equal(scriptHash, storageRecord.ScriptHash);
            Assert.Equal(source.Length, storageRecord.Storages.Count);
        }

        static void StorageRecord_WriteWithStorageItemArrayHeader(
            IBufferWriter<byte> writer, MessagePackSerializerOptions options, UInt160 scriptHash,
            IEnumerable<(StorageKey key, StorageItem item)> storages)
        {
            var count = storages.Count();
            if (count <= 0)
                return;

            var byteArrayFormatter = options.Resolver.GetFormatterWithVerify<byte[]>();
            var storageItemFormatter = options.Resolver.GetFormatterWithVerify<StorageItem>();

            var mpWriter = new MessagePackWriter(writer);
            mpWriter.WriteArrayHeader(2);
            mpWriter.WriteInt32(StorageRecord.RecordKey);
            mpWriter.WriteArrayHeader(2);
            options.Resolver.GetFormatterWithVerify<UInt160>().Serialize(ref mpWriter, scriptHash, options);
            mpWriter.WriteMapHeader(count);
            foreach (var (key, item) in storages)
            {
                byteArrayFormatter.Serialize(ref mpWriter, key.Key.ToArray(), options);

                mpWriter.WriteArrayHeader(1);
                mpWriter.Write(item.Value.Span);
            }
            mpWriter.Flush();
        }

        [Fact]
        public void can_deserialize_uint160_raw()
        {
            var scriptHash = UInt160.Parse("0001020304050607080900010203040506070809");

            var writer = new ArrayBufferWriter<byte>();
            {
                var mpWriter = new MessagePackWriter(writer);
                mpWriter.WriteRaw(scriptHash.ToArray().AsSpan(0, UInt160.Length));
                mpWriter.Flush();
            }

            var actual = MessagePackSerializer.Deserialize<UInt160>(writer.WrittenMemory, options, TestContext.Current.CancellationToken);

            Assert.Equal(scriptHash, actual);
        }

        [Fact]
        public void can_deserialize_uint160_not_raw()
        {
            var scriptHash = UInt160.Parse("0001020304050607080900010203040506070809");

            var writer = new ArrayBufferWriter<byte>();
            MessagePackSerializer.Serialize<UInt160>(writer, scriptHash, options, TestContext.Current.CancellationToken);

            var actual = MessagePackSerializer.Deserialize<UInt160>(writer.WrittenMemory, options, TestContext.Current.CancellationToken);

            Assert.Equal(scriptHash, actual);
        }
    }
}
