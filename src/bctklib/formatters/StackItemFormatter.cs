// Copyright (C) 2015-2024 The Neo Project.
//
// StackItemFormatter.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Numerics;
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
using PrimitiveType = Neo.VM.Types.PrimitiveType;
using StackItem = Neo.VM.Types.StackItem;
using StackItemType = Neo.VM.Types.StackItemType;
using TraceInteropInterface = Neo.BlockchainToolkit.TraceDebug.TraceInteropInterface;

namespace MessagePack.Formatters.Neo.BlockchainToolkit
{
    public class StackItemFormatter : IMessagePackFormatter<StackItem>
    {
        public static readonly StackItemFormatter Instance = new StackItemFormatter();

        public StackItem Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var count = reader.ReadArrayHeader();
            if (count != 2)
                throw new MessagePackSerializationException($"Invalid StackItem Array Header {count}");

            var type = options.Resolver.GetFormatterWithVerify<StackItemType>().Deserialize(ref reader, options);
            switch (type)
            {
                case StackItemType.Any:
                    reader.ReadNil();
                    return StackItem.Null;
                case StackItemType.Boolean:
                    return reader.ReadBoolean() ? StackItem.True : StackItem.False;
                case StackItemType.Buffer:
                    {
                        var bytes = options.Resolver.GetFormatter<byte[]>()!.Deserialize(ref reader, options);
                        return new NeoBuffer(bytes);
                    }
                case StackItemType.ByteString:
                    {
                        var bytes = options.Resolver.GetFormatter<byte[]>()!.Deserialize(ref reader, options);
                        return new NeoByteString(bytes);
                    }
                case StackItemType.Integer:
                    {
                        var integer = options.Resolver.GetFormatterWithVerify<BigInteger>().Deserialize(ref reader, options);
                        return new NeoInteger(integer);
                    }
                case StackItemType.InteropInterface:
                    {
                        var typeName = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                        return new TraceInteropInterface(typeName);
                    }
                case StackItemType.Pointer:
                    reader.ReadNil();
                    return new NeoPointer(Array.Empty<byte>(), 0);
                case StackItemType.Map:
                    {
                        var map = new NeoMap();
                        var mapCount = reader.ReadMapHeader();
                        for (int i = 0; i < mapCount; i++)
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
                        var arrayCount = reader.ReadArrayHeader();
                        for (int i = 0; i < arrayCount; i++)
                        {
                            array.Add(Deserialize(ref reader, options));
                        }
                        return array;
                    }
            }

            throw new MessagePackSerializationException($"Invalid StackItem {type}");
        }

        public void Serialize(ref MessagePackWriter writer, StackItem value, MessagePackSerializerOptions options)
        {
            var resolver = options.Resolver;
            var stackItemTypeResolver = resolver.GetFormatterWithVerify<StackItemType>();

            writer.WriteArrayHeader(2);
            switch (value)
            {
                case NeoBoolean _:
                    stackItemTypeResolver.Serialize(ref writer, StackItemType.Boolean, options);
                    writer.Write(value.GetBoolean());
                    break;
                case NeoBuffer buffer:
                    stackItemTypeResolver.Serialize(ref writer, StackItemType.Buffer, options);
                    writer.Write(buffer.InnerBuffer.Span);
                    break;
                case NeoByteString byteString:
                    stackItemTypeResolver.Serialize(ref writer, StackItemType.ByteString, options);
                    writer.Write(byteString.GetSpan());
                    break;
                case NeoInteger integer:
                    stackItemTypeResolver.Serialize(ref writer, StackItemType.Integer, options);
                    resolver.GetFormatterWithVerify<BigInteger>().Serialize(ref writer, integer.GetInteger(), options);
                    break;
                case NeoInteropInterface interopInterface:
                    {
                        stackItemTypeResolver.Serialize(ref writer, StackItemType.InteropInterface, options);
                        var typeName = interopInterface.GetInterface<object>().GetType().FullName ?? "<unknown InteropInterface>";
                        resolver.GetFormatterWithVerify<string>().Serialize(ref writer, typeName, options);
                    }
                    break;
                case NeoMap map:
                    stackItemTypeResolver.Serialize(ref writer, StackItemType.Map, options);
                    writer.WriteMapHeader(map.Count);
                    foreach (var kvp in map)
                    {
                        Serialize(ref writer, kvp.Key, options);
                        Serialize(ref writer, kvp.Value, options);
                    }
                    break;
                case NeoNull _:
                    stackItemTypeResolver.Serialize(ref writer, StackItemType.Any, options);
                    writer.WriteNil();
                    break;
                case NeoPointer _:
                    stackItemTypeResolver.Serialize(ref writer, StackItemType.Pointer, options);
                    writer.WriteNil();
                    break;
                case NeoArray array:
                    {
                        var stackItemType = array is NeoStruct ? StackItemType.Struct : StackItemType.Array;
                        stackItemTypeResolver.Serialize(ref writer, stackItemType, options);
                        writer.WriteArrayHeader(array.Count);
                        for (int i = 0; i < array.Count; i++)
                        {
                            Serialize(ref writer, array[i], options);
                        }
                        break;
                    }
                default:
                    throw new MessagePackSerializationException($"Invalid StackItem {value.GetType()}");
            }
        }
    }
}
