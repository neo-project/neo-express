using System;
using System.Buffers;
using MessagePack;
using MessagePack.Formatters;
using Neo.IO;

namespace Neo.Seattle.TraceDebug.Formatters
{
    public class UInt160Formatter : IMessagePackFormatter<UInt160>
    {
        public static readonly UInt160Formatter Instance = new UInt160Formatter();

        public UInt160 Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null!;
            }

            var seq = reader.ReadRaw(UInt160.Length);
            if (seq.Length == UInt160.Length)
            {
                // TODO: avoid array creation by adding ReadOnlySequence<byte> ctor to uint160
                return new UInt160(seq.ToArray());
            }

            throw new MessagePackSerializationException("Invalid UInt160");
        }

        public void Serialize(ref MessagePackWriter writer, UInt160 value, MessagePackSerializerOptions options)
        {
            // TODO: avoid array creation by adding AsSpan method to uint160
            writer.WriteRaw(value.ToArray().AsSpan(0, UInt160.Length));
        }
    }
}
