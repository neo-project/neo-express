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
using NeoInteger  = Neo.VM.Types.Integer;
using NeoInteropInterface = Neo.VM.Types.InteropInterface;
using NeoMap  = Neo.VM.Types.Map;
using NeoNull  = Neo.VM.Types.Null;
using NeoPointer  = Neo.VM.Types.Pointer;
using NeoStruct  = Neo.VM.Types.Struct;

namespace Neo.Seattle.TraceDebug.Models
{
    public class StackItemFormatter : IMessagePackFormatter<StackItem>
    {
        public StackItem Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var type = options.Resolver.GetFormatterWithVerify<StackItemType>().Deserialize(ref reader, options);

            switch (type)
            {
                case StackItemType.Any:
                    return StackItem.Null;
                case StackItemType.Boolean:
                    return reader.ReadBoolean()
                        ? StackItem.True
                        : StackItem.False;
                case StackItemType.Buffer:
                {
                    var bytes = reader.ReadBytes();
                    if (!bytes.HasValue) throw new MessagePackSerializationException("Invalid Buffer");
                    return new NeoBuffer(bytes.Value.ToArray());
                }
                case StackItemType.ByteString:
                {
                    var bytes = reader.ReadBytes();
                    if (!bytes.HasValue) throw new MessagePackSerializationException("Invalid ByteString");
                    return new NeoByteString(bytes.Value.ToArray());
                }
                case StackItemType.Integer:
                {
                    var integer = options.Resolver.GetFormatterWithVerify<BigInteger>().Deserialize(ref reader, options);
                    return new NeoInteger(integer);
                }
                case StackItemType.Map:
                {
                    var count = reader.ReadMapHeader();
                    var map = new NeoMap();
                    for (int i = 0; i < count; i++)
                    {
                        var key = (PrimitiveType)Deserialize(ref reader, options);
                        map[key] = Deserialize(ref reader, options);
                    }
                    return map;
                }
                case StackItemType.Array:
                case StackItemType.Struct:
                {
                    var count = reader.ReadArrayHeader();
                    var array = type == StackItemType.Array
                        ? new NeoArray()
                        : new NeoStruct();
                    for (int i = 0; i < count; i++)
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
            var formatter = options.Resolver.GetFormatterWithVerify<StackItemType>();

            switch (value)
            {
                case NeoBoolean _:
                    formatter.Serialize(ref writer, StackItemType.Boolean, options);
                    writer.Write(value.GetBoolean());
                    break;
                case NeoBuffer buffer:
                    formatter.Serialize(ref writer, StackItemType.Buffer, options);
                    writer.Write(buffer.InnerBuffer.AsSpan());
                    break;
                case NeoByteString byteString:
                    formatter.Serialize(ref writer, StackItemType.ByteString, options);
                    writer.Write(byteString);
                    break;
                case NeoInteger integer:
                    formatter.Serialize(ref writer, StackItemType.Integer, options);
                    options.Resolver.GetFormatterWithVerify<BigInteger>().Serialize(ref writer, integer.GetInteger(), options);
                    break;
                case NeoInteropInterface interopInterface:
                    formatter.Serialize(ref writer, StackItemType.InteropInterface, options);
                    break;
                case NeoMap map:
                    formatter.Serialize(ref writer, StackItemType.Map, options);
                    writer.WriteMapHeader(map.Count);
                    foreach (var kvp in map)
                    {
                        this.Serialize(ref writer, kvp.Key, options);
                        this.Serialize(ref writer, kvp.Value, options);
                    }
                    break;
                case NeoNull _:
                    formatter.Serialize(ref writer, StackItemType.Any, options);
                    break;
                case NeoPointer pointer:
                {
                    var scriptHash = Neo.SmartContract.Helper.ToScriptHash(pointer.Script);
                    formatter.Serialize(ref writer, StackItemType.Pointer, options);
                    options.Resolver.GetFormatterWithVerify<UInt160>().Serialize(ref writer, scriptHash, options);
                    writer.Write(pointer.Position);
                    break;
                }
                case NeoArray array:
                {
                    var type = array is NeoStruct ? StackItemType.Struct : StackItemType.Array;
                    formatter.Serialize(ref writer, type, options);
                    writer.WriteArrayHeader(array.Count);
                    for (int i = 0; i < array.Count; i++)
                    {
                        this.Serialize(ref writer, array[i], options);
                    }
                    break;
                }
            }
        }
    }
}
