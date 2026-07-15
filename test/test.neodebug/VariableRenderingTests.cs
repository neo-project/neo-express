// Copyright (C) 2015-2026 The Neo Project.
//
// VariableRenderingTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract;
using Neo.VM;
using NeoDebug.Neo3;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using NeoArray = Neo.VM.Types.Array;
using NeoMap = Neo.VM.Types.Map;
using NeoStruct = Neo.VM.Types.Struct;
using Script = Neo.VM.Script;
using StackItem = Neo.VM.Types.StackItem;

namespace test.neodebug
{
    public class VariableRenderingTests
    {
        private readonly VariableManager _manager = new();

        private List<Variable> Expand(Variable parent)
        {
            Assert.True(_manager.TryGet(parent.VariablesReference, out var container));
            return container!.Enumerate(_manager).ToList();
        }

        [Fact]
        public void integer_renders_with_the_integer_type()
        {
            var variable = ((StackItem)new Neo.VM.Types.Integer(42)).ToVariable(_manager, "n");

            Assert.Equal("n", variable.Name);
            Assert.Equal("42", variable.Value);
            Assert.Equal("Integer", variable.Type); // regression: the reference mislabeled integers as "Boolean".
        }

        [Fact]
        public void boolean_null_and_pointer_render_their_types()
        {
            Assert.Equal("Boolean", StackItem.True.ToVariable(_manager, "b").Type);
            Assert.Equal("True", StackItem.True.ToVariable(_manager, "b").Value);

            var @null = StackItem.Null.ToVariable(_manager, "z");
            Assert.Equal("Null", @null.Type);
            Assert.Equal("<null>", @null.Value);

            var pointer = ((StackItem)new Neo.VM.Types.Pointer(new Script(new byte[] { (byte)OpCode.RET }), 0)).ToVariable(_manager, "p");
            Assert.Equal("Pointer", pointer.Value);
        }

        [Fact]
        public void byte_string_expands_into_per_byte_children()
        {
            var bytes = new byte[] { 0x01, 0x0a, 0xff };
            var variable = ((StackItem)new Neo.VM.Types.ByteString(bytes)).ToVariable(_manager, "data");

            Assert.Equal("ByteString[3]", variable.Value);
            Assert.Equal(3, variable.IndexedVariables);

            var children = Expand(variable);
            Assert.Equal(3, children.Count);
            Assert.Equal("0xff", children[2].Value);
            Assert.Equal("Byte", children[0].Type);
        }

        [Fact]
        public void array_expands_into_indexed_children()
        {
            var array = new NeoArray(new StackItem[] { new Neo.VM.Types.Integer(1), new Neo.VM.Types.Integer(2) });
            var variable = ((StackItem)array).ToVariable(_manager, "arr");

            Assert.Equal("Array[2]", variable.Value);
            Assert.Equal(2, variable.IndexedVariables);
            Assert.Equal(2, Expand(variable).Count);
        }

        [Fact]
        public void struct_preserves_its_type_while_using_indexed_children()
        {
            var @struct = new NeoStruct { new Neo.VM.Types.Integer(1) };
            var variable = ((StackItem)@struct).ToVariable(_manager, "value");

            Assert.Equal("Struct[1]", variable.Value);
            Assert.Equal(1, variable.IndexedVariables);
            Assert.Single(Expand(variable));
        }

        [Fact]
        public void map_renders_mixed_primitive_keys_without_throwing()
        {
            var map = new NeoMap
            {
                [new Neo.VM.Types.Integer(7)] = new Neo.VM.Types.Integer(70),
                [new Neo.VM.Types.ByteString(new byte[] { 0xab })] = StackItem.True,
            };
            var variable = ((StackItem)map).ToVariable(_manager, "m");

            Assert.Equal("Map[2]", variable.Value);
            var children = Expand(variable);
            Assert.Equal(2, children.Count);
            Assert.Contains(children, c => c.Name == "7");
            Assert.Contains(children, c => c.Name == "ab");
        }

        [Fact]
        public void variable_manager_hands_out_increasing_nonzero_ids_and_resets()
        {
            var a = new SlotContainer("#a", Array.Empty<StackItem>());
            var b = new SlotContainer("#b", Array.Empty<StackItem>());

            var idA = _manager.Add(a);
            var idB = _manager.Add(b);
            Assert.Equal(1, idA);
            Assert.Equal(2, idB);
            Assert.True(_manager.TryGet(idA, out var got) && ReferenceEquals(got, a));

            _manager.Clear();
            Assert.False(_manager.TryGet(idA, out _));
            Assert.Equal(1, _manager.Add(a)); // ids reset after a clear
        }

        [Fact]
        public void storage_container_renders_one_entry_per_row_expandable_to_key_and_item()
        {
            var rows = new (ReadOnlyMemory<byte> key, StorageItem item)[]
            {
                (new byte[] { 0x01 }, new StorageItem(new byte[] { 0x02 })),
            };
            var container = new StorageContainer(rows);

            var entries = container.Enumerate(_manager).ToList();
            Assert.Single(entries);
            Assert.Equal(2, entries[0].NamedVariables);

            var kvp = Expand(entries[0]);
            Assert.Equal(2, kvp.Count);
            Assert.Equal("key", kvp[0].Name);
            Assert.Equal("item", kvp[1].Name);
        }

        [Fact]
        public void storage_container_rejects_short_evaluate_expressions()
        {
            var container = new StorageContainer(Array.Empty<(ReadOnlyMemory<byte> key, StorageItem item)>());

            Assert.Throws<InvalidOperationException>(() => container.Evaluate("#storage[00000000]".AsMemory()));
        }

        [Fact]
        public void execution_context_renders_named_locals_from_debug_info()
        {
            var context = new FakeContext
            {
                InstructionPointer = 5,
                LocalVariables = new StackItem[] { new Neo.VM.Types.Integer(99) },
            };
            var method = new DebugInfo.Method(
                Id: "M", Namespace: "N", Name: "Run", Range: (0, 100), ReturnType: "Void",
                Parameters: Array.Empty<DebugInfo.SlotVariable>(),
                Variables: new[] { new DebugInfo.SlotVariable("count", "Integer", 0) },
                SequencePoints: Array.Empty<DebugInfo.SequencePoint>());
            var debugInfo = new DebugInfo(UInt160.Zero, "", Array.Empty<string>(), new[] { method },
                Array.Empty<DebugInfo.Event>(), Array.Empty<DebugInfo.SlotVariable>());

            var variables = new ExecutionContextContainer(context, debugInfo).Enumerate(_manager).ToList();

            Assert.Contains(variables, v => v.Name == "count" && v.Value == "99");
        }

        private sealed class FakeContext : IExecutionContext
        {
            public Instruction? CurrentInstruction => null;
            public int InstructionPointer { get; set; }
            public UInt160 ScriptHash => UInt160.Zero;
            public UInt160 ScriptIdentifier => UInt160.Zero;
            public Script Script => new(Array.Empty<byte>());
            public MethodToken[] Tokens => Array.Empty<MethodToken>();
            public IReadOnlyList<StackItem> EvaluationStack { get; set; } = Array.Empty<StackItem>();
            public IReadOnlyList<StackItem> LocalVariables { get; set; } = Array.Empty<StackItem>();
            public IReadOnlyList<StackItem> StaticFields { get; set; } = Array.Empty<StackItem>();
            public IReadOnlyList<StackItem> Arguments { get; set; } = Array.Empty<StackItem>();
        }
    }
}
