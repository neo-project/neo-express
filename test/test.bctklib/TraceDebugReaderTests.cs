// Copyright (C) 2015-2026 The Neo Project.
//
// TraceDebugReaderTests.cs file belongs to neo-express project and is free
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
using Neo.SmartContract;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using StackItem = Neo.VM.Types.StackItem;

namespace test.bctklib
{
    public class TraceDebugReaderTests
    {
        static readonly UInt160 ContractHash = UInt160.Parse("0x0000000000000000000000000000000000000001");
        static readonly Script ContractScript = new(new byte[] { (byte)OpCode.PUSH1, (byte)OpCode.RET });

        // ProtocolSettings(7) + Script(5) are side records; Trace(0)/Storage(6)/Results(3) are returned.
        static byte[] SampleTrace() => Serialize(
            new ProtocolSettingsRecord(0x1234u, 0x35),
            new ScriptRecord(ContractHash, ContractScript),
            Trace(VMState.NONE, 100, instructionPointer: 0),
            new StorageRecord(ContractHash, new Dictionary<byte[], StorageItem> { [new byte[] { 0x10 }] = new StorageItem(new byte[] { 0x20 }) }),
            Trace(VMState.NONE, 150, instructionPointer: 2),
            new ResultsRecord(VMState.HALT, 200, new StackItem[] { new Neo.VM.Types.Integer(42) }));

        static TraceRecord Trace(VMState state, long gas, int instructionPointer)
        {
            var frame = new TraceRecord.StackFrame(
                ContractHash, ContractHash, instructionPointer, hasCatch: false,
                evaluationStack: new StackItem[] { new Neo.VM.Types.Integer(7) },
                localVariables: Array.Empty<StackItem>(),
                staticFields: Array.Empty<StackItem>(),
                arguments: Array.Empty<StackItem>());
            return new TraceRecord(state, gas, new[] { frame });
        }

        static byte[] Serialize(params ITraceDebugRecord[] records)
        {
            var options = MessagePackSerializerOptions.Standard.WithResolver(TraceDebugResolver.Instance);
            using var ms = new MemoryStream();
            foreach (var record in records)
                MessagePackSerializer.Serialize<ITraceDebugRecord>(ms, record, options);
            return ms.ToArray();
        }

        static TraceDebugReader Open(byte[] trace) => new(new MemoryStream(trace), leaveOpen: false);

        [Fact]
        public void returns_main_records_in_order_and_absorbs_side_records()
        {
            using var reader = Open(SampleTrace());

            var kinds = new List<string>();
            while (reader.TryGetNext(out var record))
                kinds.Add(record.GetType().Name);

            Assert.Equal(
                new[] { nameof(TraceRecord), nameof(StorageRecord), nameof(TraceRecord), nameof(ResultsRecord) },
                kinds);
        }

        [Fact]
        public void lifts_network_and_address_version_from_protocol_settings()
        {
            using var reader = Open(SampleTrace());

            Assert.True(reader.TryGetNext(out _));
            Assert.Equal(0x1234u, reader.Network);
            Assert.Equal((byte)0x35, reader.AddressVersion);
        }

        [Fact]
        public void resolves_contract_script_from_script_record()
        {
            using var reader = Open(SampleTrace());
            reader.TryGetNext(out _);

            Assert.True(reader.TryGetContract(ContractHash, out var script));
            Assert.NotNull(script);
            Assert.Equal(ContractScript.Length, script!.Length);
            Assert.False(reader.TryGetContract(UInt160.Zero, out _));
        }

        [Fact]
        public void seeded_contracts_resolve_before_their_script_record()
        {
            var seed = new[] { new KeyValuePair<UInt160, Script>(UInt160.Zero, ContractScript) };
            using var reader = new TraceDebugReader(new MemoryStream(SampleTrace()), leaveOpen: false, seed);

            Assert.True(reader.TryGetContract(UInt160.Zero, out _));
        }

        [Fact]
        public void at_start_is_true_only_before_the_first_record()
        {
            using var reader = Open(SampleTrace());
            Assert.True(reader.AtStart);

            Assert.True(reader.TryGetNext(out _));
            Assert.False(reader.AtStart);
        }

        [Fact]
        public void steps_backward_in_reverse_order_to_the_start()
        {
            using var reader = Open(SampleTrace());
            while (reader.TryGetNext(out _))
            {
            }

            var backward = new List<string>();
            while (reader.TryGetPrev(out var record))
                backward.Add(record.GetType().Name);

            Assert.Equal(
                new[] { nameof(ResultsRecord), nameof(TraceRecord), nameof(StorageRecord), nameof(TraceRecord) },
                backward);
            Assert.True(reader.AtStart);
            Assert.False(reader.TryGetPrev(out _));
        }

        [Fact]
        public void find_storage_returns_entries_for_the_current_position()
        {
            using var reader = Open(SampleTrace());

            // Advance to the storage record (second returned record).
            reader.TryGetNext(out _);
            reader.TryGetNext(out _);

            var entries = reader.FindStorage(ContractHash).ToList();
            Assert.Single(entries);
            Assert.Equal(new byte[] { 0x10 }, entries[0].key.ToArray());
            Assert.Equal(new byte[] { 0x20 }, entries[0].value.Value.ToArray());

            Assert.Empty(reader.FindStorage(UInt160.Zero));
        }

        [Fact]
        public void preserves_trace_record_state_and_gas()
        {
            using var reader = Open(SampleTrace());

            Assert.True(reader.TryGetNext(out var first));
            var trace = Assert.IsType<TraceRecord>(first);
            Assert.Equal(VMState.NONE, trace.State);
            Assert.Equal(100, trace.GasConsumed);
            Assert.Single(trace.StackFrames);
            Assert.Equal(0, trace.StackFrames[0].InstructionPointer);
        }

        [Fact]
        public void next_after_end_keeps_history_intact_for_step_back()
        {
            using var reader = Open(SampleTrace());
            while (reader.TryGetNext(out _))
            {
            }

            // A failed next must not corrupt the cursor: stepping back still yields the last record.
            Assert.False(reader.TryGetNext(out _));
            Assert.True(reader.TryGetPrev(out var record));
            Assert.IsType<ResultsRecord>(record);
        }
    }
}
