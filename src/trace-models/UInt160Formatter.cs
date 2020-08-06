using System;
using System.Buffers;
using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;
using Neo.IO;
using Neo.Ledger;
using Neo.VM;
using StackItem = Neo.VM.Types.StackItem;

namespace Neo.Seattle.TraceDebug.Models
{
    public class UInt160Formatter : IMessagePackFormatter<UInt160>
    {
        public UInt160 Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var seq = reader.ReadRaw(UInt160.Length);
            if (seq.Length == UInt160.Length)
            {
                return new UInt160(seq.ToArray());
            }

            throw new MessagePackSerializationException("Invalid UInt160");
        }

        public void Serialize(ref MessagePackWriter writer, UInt160 value, MessagePackSerializerOptions options)
        {
            writer.WriteRaw(value.ToArray().AsSpan(0, UInt160.Length));
        }
    }
}
