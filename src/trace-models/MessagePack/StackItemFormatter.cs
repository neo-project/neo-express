using System;
using System.Buffers;
using System.Numerics;
using MessagePack;
using MessagePack.Formatters;

using StackItem = Neo.VM.Types.StackItem;
using StackItemType = Neo.VM.Types.StackItemType;
using PrimitiveType = Neo.VM.Types.PrimitiveType;

using NeoArray = Neo.VM.Types.Array;
using NeoBoolean = Neo.VM.Types.Boolean;
using NeoBuffer = Neo.VM.Types.Buffer;
using NeoByteString = Neo.VM.Types.ByteString;
using NeoInteger = Neo.VM.Types.Integer;
using NeoInteropInterface = Neo.VM.Types.InteropInterface;
using NeoMap = Neo.VM.Types.Map;
using NeoNull = Neo.VM.Types.Null;
using NeoPointer = Neo.VM.Types.Pointer;
using NeoStruct = Neo.VM.Types.Struct;

namespace Neo.Seattle.TraceDebug.Formatters
{
    public class StackItemFormatter : IMessagePackFormatter<StackItem>
    {
        public static readonly StackItemFormatter Instance = new StackItemFormatter();

        public StackItem Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var count = reader.ReadArrayHeader();
            if (count != 2) throw new MessagePackSerializationException();

            var type = options.Resolver.GetFormatterWithVerify<StackItemType>().Deserialize(ref reader, options);

            switch (type)
            {
                case StackItemType.Any:
                    reader.ReadNil();
                    return StackItem.Null;
                case StackItemType.Boolean:
                    return reader.ReadBoolean()
                        ? StackItem.True
                        : StackItem.False;
                case StackItemType.Buffer:
                    {
                        var bytes = reader.ReadBytes();
                        return bytes.HasValue
                            ? new NeoBuffer(bytes.Value.ToArray())
                            : throw new MessagePackSerializationException("Invalid Buffer");
                    }
                case StackItemType.ByteString:
                    {
                        var bytes = reader.ReadBytes();
                        return bytes.HasValue
                            ? new NeoByteString(bytes.Value.ToArray())
                            : throw new MessagePackSerializationException("Invalid ByteString");
                    }
                case StackItemType.Integer:
                    {
                        var integer = options.Resolver.GetFormatterWithVerify<BigInteger>().Deserialize(ref reader, options);
                        return new NeoInteger(integer);
                    }
                case StackItemType.InteropInterface:
                    throw new NotImplementedException();
                case StackItemType.Pointer:
                    throw new NotImplementedException();
                case StackItemType.Map:
                    {
                        var map = new NeoMap();
                        for (int i = 0; i < reader.ReadMapHeader(); i++)
                        {
                            var key = (PrimitiveType)Deserialize(ref reader, options);
                            map[key] = Deserialize(ref reader, options);
                        }
                        return map;
                    }
                case StackItemType.Array:
                case StackItemType.Struct:
                    {
                        var array = type == StackItemType.Array
                            ? new NeoArray()
                            : new NeoStruct();
                        for (int i = 0; i < reader.ReadArrayHeader(); i++)
                        {
                            array.Add(Deserialize(ref reader, options));
                        }
                        return array;
                    }
            }

            throw new MessagePackSerializationException("Invalid StackItem");
        }

        public void Serialize(ref MessagePackWriter writer, StackItem value, MessagePackSerializerOptions options)
        {
            var stackItemTypeFormatter = options.Resolver.GetFormatterWithVerify<StackItemType>();

            switch (value)
            {
                case NeoBoolean _:
                    writer.WriteArrayHeader(2);
                    stackItemTypeFormatter.Serialize(ref writer, StackItemType.Boolean, options);
                    writer.Write(value.GetBoolean());
                    break;
                case NeoBuffer buffer:
                    writer.WriteArrayHeader(2);
                    stackItemTypeFormatter.Serialize(ref writer, StackItemType.Buffer, options);
                    writer.Write(buffer.InnerBuffer.AsSpan());
                    break;
                case NeoByteString byteString:
                    writer.WriteArrayHeader(2);
                    stackItemTypeFormatter.Serialize(ref writer, StackItemType.ByteString, options);
                    writer.Write(byteString);
                    break;
                case NeoInteger integer:
                    writer.WriteArrayHeader(2);
                    stackItemTypeFormatter.Serialize(ref writer, StackItemType.Integer, options);
                    options.Resolver.GetFormatterWithVerify<BigInteger>().Serialize(ref writer, integer.GetInteger(), options);
                    break;
                case NeoInteropInterface interopInterface:
                    stackItemTypeFormatter.Serialize(ref writer, StackItemType.InteropInterface, options);
                    throw new NotImplementedException();
                case NeoMap map:
                    writer.WriteArrayHeader(2);
                    stackItemTypeFormatter.Serialize(ref writer, StackItemType.Map, options);
                    writer.WriteMapHeader(map.Count);
                    foreach (var kvp in map)
                    {
                        Serialize(ref writer, kvp.Key, options);
                        Serialize(ref writer, kvp.Value, options);
                    }
                    break;
                case NeoNull _:
                    writer.WriteArrayHeader(2);
                    stackItemTypeFormatter.Serialize(ref writer, StackItemType.Any, options);
                    writer.WriteNil();
                    break;
                case NeoPointer pointer:
                    stackItemTypeFormatter.Serialize(ref writer, StackItemType.Pointer, options);
                    throw new NotImplementedException();
                case NeoArray array:
                    {
                        var type = array is NeoStruct ? StackItemType.Struct : StackItemType.Array;
                        writer.WriteArrayHeader(2);
                        stackItemTypeFormatter.Serialize(ref writer, type, options);
                        writer.WriteArrayHeader(array.Count);
                        for (int i = 0; i < array.Count; i++)
                        {
                            Serialize(ref writer, array[i], options);
                        }
                        break;
                    }
            }
        }
    }
}
