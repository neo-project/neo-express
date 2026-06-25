// Copyright (C) 2015-2026 The Neo Project.
//
// TraceReplayEngineTests.cs file belongs to neo-express project and is free
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
using NeoDebug.Neo3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using StackItem = Neo.VM.Types.StackItem;

namespace test.neodebug
{
    public class TraceReplayEngineTests
    {
        static readonly UInt160 ContractHash = UInt160.Parse("0x0000000000000000000000000000000000000001");

        // A script whose single-byte opcodes make instruction pointers 0, 2 and 4 all valid boundaries.
        static readonly Script ContractScript = new(new byte[]
        {
            (byte)OpCode.NOP, (byte)OpCode.NOP, (byte)OpCode.NOP,
            (byte)OpCode.NOP, (byte)OpCode.NOP, (byte)OpCode.RET,
        });

        // ProtocolSettings(7) + Script(5) prime the engine; the three TraceRecords are the VM steps,
        // with a StorageRecord and a ResultsRecord interleaved as the producer would emit them.
        static byte[] SampleTrace() => Serialize(
            new ProtocolSettingsRecord(0x004F454Eu, 0x35),
            new ScriptRecord(ContractHash, ContractScript),
            Trace(VMState.NONE, 10, instructionPointer: 0),
            new StorageRecord(ContractHash, new Dictionary<byte[], StorageItem> { [new byte[] { 0xAA }] = new StorageItem(new byte[] { 0xBB }) }),
            Trace(VMState.NONE, 20, instructionPointer: 2),
            new ResultsRecord(VMState.HALT, 30, new StackItem[] { new Neo.VM.Types.Integer(42) }),
            Trace(VMState.HALT, 30, instructionPointer: 4));

        static TraceRecord Trace(VMState state, long gas, int instructionPointer)
        {
            // ScriptHash and ScriptIdentifier both equal the contract hash so the engine resolves the
            // frame's script from the trace's script record (current bctklib keys both on CalculateScriptHash).
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

        static TraceReplayEngine Open() => new(new TraceDebugReader(new MemoryStream(SampleTrace())));

        [Fact]
        public void opens_positioned_at_the_entry_step()
        {
            using var engine = Open();

            Assert.True(engine.SupportsStepBack);
            Assert.Equal((byte)0x35, engine.AddressVersion);
            Assert.Equal(VMState.NONE, engine.State);
            Assert.Single(engine.InvocationStack);
            Assert.NotNull(engine.CurrentContext);
            Assert.Equal(0, engine.CurrentContext!.InstructionPointer);
            Assert.Null(engine.FaultException);
        }

        [Fact]
        public void steps_forward_through_the_recorded_instructions()
        {
            using var engine = Open();

            Assert.True(engine.ExecuteNextInstruction());
            Assert.Equal(2, engine.CurrentContext!.InstructionPointer);

            Assert.True(engine.ExecuteNextInstruction());
            Assert.Equal(4, engine.CurrentContext!.InstructionPointer);
            Assert.Equal(VMState.HALT, engine.State);
            Assert.Single(engine.ResultStack);
            Assert.Equal(42, (int)engine.ResultStack[0].GetInteger());

            Assert.False(engine.ExecuteNextInstruction());
        }

        [Fact]
        public void steps_backward_for_time_travel()
        {
            using var engine = Open();
            while (engine.ExecuteNextInstruction())
            { }

            Assert.True(engine.ExecutePrevInstruction());
            Assert.Equal(2, engine.CurrentContext!.InstructionPointer);
            // Stepping back past the results record clears the result stack again.
            Assert.Empty(engine.ResultStack);

            Assert.True(engine.ExecutePrevInstruction());
            Assert.Equal(0, engine.CurrentContext!.InstructionPointer);
            Assert.True(engine.AtStart);
            Assert.False(engine.ExecutePrevInstruction());
        }

        [Fact]
        public void resolves_the_frame_script_from_the_trace()
        {
            using var engine = Open();

            Assert.Equal(ContractScript.Length, engine.CurrentContext!.Script.Length);
            Assert.NotNull(engine.CurrentContext.CurrentInstruction);
            Assert.True(engine.TryGetContract(ContractHash, out _));
        }

        [Fact]
        public void exposes_contract_storage_at_the_current_position()
        {
            using var engine = Open();

            // Advance past the storage record (emitted between the first and second step).
            engine.ExecuteNextInstruction();

            var storages = engine.GetStorages(ContractHash).ToList();
            Assert.Single(storages);
            Assert.Equal(new byte[] { 0xAA }, storages[0].key.ToArray());
            Assert.Equal(new byte[] { 0xBB }, storages[0].item.Value.ToArray());
            Assert.Empty(engine.GetStorages(UInt160.Zero));
        }

        [Fact]
        public void exposes_frame_slots_and_has_no_catch_block()
        {
            using var engine = Open();

            Assert.False(engine.CatchBlockOnStack());
            Assert.Single(engine.CurrentContext!.EvaluationStack);
            Assert.Equal(7, (int)engine.CurrentContext.EvaluationStack[0].GetInteger());
            Assert.Empty(engine.CurrentContext.LocalVariables);
        }
    }
}
