// Copyright (C) 2015-2026 The Neo Project.
//
// StackItemFormatterTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using MessagePack;
using MessagePack.Formatters.Neo.BlockchainToolkit;
using MessagePack.Resolvers;
using System.Buffers;
using Xunit;
using StackItemType = Neo.VM.Types.StackItemType;

namespace test.bctklib
{
    public class StackItemFormatterTests
    {
        [Fact]
        public void Deserialize_bounds_recursion_depth()
        {
            var options = MessagePackSerializerOptions.Standard.WithResolver(TraceDebugResolver.Instance);
            var typeFormatter = options.Resolver.GetFormatterWithVerify<StackItemType>();

            // Build a StackItem payload nested deeper than the allowed depth with a loop
            // (not recursion) so constructing the test input cannot itself overflow.
            var bufferWriter = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(bufferWriter);
            // MessagePack's default MaxDepth is 500; nest beyond it to trip the guard.
            const int depth = 600;
            for (int i = 0; i < depth; i++)
            {
                writer.WriteArrayHeader(2);
                typeFormatter.Serialize(ref writer, StackItemType.Array, options);
                writer.WriteArrayHeader(1);
            }
            writer.WriteArrayHeader(2);
            typeFormatter.Serialize(ref writer, StackItemType.Any, options);
            writer.WriteNil();
            writer.Flush();
            var bytes = bufferWriter.WrittenMemory.ToArray();

            var act = () =>
            {
                var reader = new MessagePackReader(bytes);
                StackItemFormatter.Instance.Deserialize(ref reader, options);
            };

            act.Should().Throw<MessagePackSerializationException>();
        }
    }
}
