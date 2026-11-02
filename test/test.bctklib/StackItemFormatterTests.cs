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
using Neo.BlockchainToolkit.TraceDebug;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using StackItemType = Neo.VM.Types.StackItemType;

// Byte layout of the regression inputs below (a ResultsRecord carrying a single
// StackItem in its ResultStack):
//   92 03            array(2) union envelope, record key 3 (ResultsRecord)
//   93 00 00         array(3) body: VMState.NONE, GasConsumed 0
//   91               array(1) ResultStack
//   92 48            array(2) StackItem envelope, type 0x48 (Map)
//   81               map(1)
//     <key>          StackItem envelope + payload
//     92 00 C0       StackItem [Any, nil] -> Null (map value)

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

        // Regression tests for fuzzing finding: a corrupted .neo-trace whose Map
        // holds a non-primitive StackItem key (Buffer 0x30 / Array 0x40) used to
        // surface an InvalidCastException from the blind cast to PrimitiveType in
        // the Map case of StackItemFormatter.Deserialize. The reader must instead
        // reject the trace with a clean, descriptive MessagePack exception.

        [Theory]
        [InlineData(new byte[] { 0x92, 0x03, 0x93, 0x00, 0x00, 0x91, 0x92, 0x48, 0x81, 0x92, 0x30, 0xC4, 0x00, 0x92, 0x00, 0xC0 })] // Buffer key
        [InlineData(new byte[] { 0x92, 0x03, 0x93, 0x00, 0x00, 0x91, 0x92, 0x48, 0x81, 0x92, 0x40, 0x90, 0x92, 0x00, 0xC0 })]     // Array key
        public void map_with_non_primitive_key_is_rejected_cleanly(byte[] trace)
        {
            var act = () =>
            {
                using var reader = new TraceDebugReader(new MemoryStream(trace, writable: false));
                while (reader.TryGetNext(out _))
                {
                }
            };

            var exception = act.Should().Throw<Exception>().Which;
            var chain = ExceptionChain(exception).ToList();

            chain.Should().NotContain(e => e is InvalidCastException);
            chain.Should().Contain(e => e is MessagePackSerializationException);
            chain.OfType<MessagePackSerializationException>()
                .Should().Contain(e => e.Message.Contains("Map key", StringComparison.Ordinal));
        }

        [Fact]
        public void map_with_bytestring_key_parses_through_reader()
        {
            // Same shape as the corrupt inputs, but the Map key is a ByteString
            // (0x28) — a valid primitive key that must keep parsing cleanly.
            byte[] trace = { 0x92, 0x03, 0x93, 0x00, 0x00, 0x91, 0x92, 0x48, 0x81, 0x92, 0x28, 0xC4, 0x01, 0x01, 0x92, 0x00, 0xC0 };

            using var reader = new TraceDebugReader(new MemoryStream(trace, writable: false));

            reader.TryGetNext(out var record).Should().BeTrue();
            var results = Assert.IsType<ResultsRecord>(record);
            var map = Assert.IsType<Neo.VM.Types.Map>(Assert.Single(results.ResultStack));
            var entry = Assert.Single(map);
            var key = Assert.IsType<Neo.VM.Types.ByteString>(entry.Key);
            Assert.Equal(new byte[] { 0x01 }, key.GetSpan().ToArray());
        }

        static IEnumerable<Exception> ExceptionChain(Exception exception)
        {
            for (var e = (Exception?)exception; e is not null; e = e.InnerException)
                yield return e;
        }
    }
}
