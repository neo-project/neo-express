// Copyright (C) 2015-2026 The Neo Project.
//
// ByteArrayContainer.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Buffer = Neo.VM.Types.Buffer;
using ByteString = Neo.VM.Types.ByteString;
using StackItemType = Neo.VM.Types.StackItemType;

namespace NeoDebug.Neo3
{
    /// <summary>Expands a byte sequence (a <see cref="ByteString"/> or <see cref="Buffer"/>) into per-byte child variables.</summary>
    internal class ByteArrayContainer : IVariableContainer
    {
        private readonly ReadOnlyMemory<byte> _memory;

        private ByteArrayContainer(ReadOnlyMemory<byte> memory)
        {
            _memory = memory;
        }

        public static Variable Create(IVariableManager manager, ByteString byteString, string name)
            => ToVariable(manager, new ByteArrayContainer(byteString), name, StackItemType.ByteString);

        public static Variable Create(IVariableManager manager, Buffer buffer, string name)
            => ToVariable(manager, new ByteArrayContainer(buffer.InnerBuffer), name, StackItemType.Buffer);

        public static Variable Create(IVariableManager manager, ReadOnlyMemory<byte> memory, string name)
            => ToVariable(manager, new ByteArrayContainer(memory), name, StackItemType.ByteString);

        private static Variable ToVariable(IVariableManager manager, ByteArrayContainer container, string name, StackItemType type)
        {
            var typeName = type.ToString();
            return new Variable()
            {
                Name = name,
                Value = $"{typeName}[{container._memory.Length}]",
                Type = typeName,
                VariablesReference = manager.Add(container),
                IndexedVariables = container._memory.Length,
            };
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            for (int i = 0; i < _memory.Length; i++)
            {
                yield return new Variable()
                {
                    Name = i.ToString(),
                    Value = "0x" + _memory.Span[i].ToString("x"),
                    Type = "Byte",
                };
            }
        }
    }
}
