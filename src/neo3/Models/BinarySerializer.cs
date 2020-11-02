using Neo.IO;
using Neo.VM;
using Neo.VM.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace NeoExpress.Neo3.Models
{
    // TODO: remove once https://github.com/neo-project/neo/pull/2041 is merged
    internal static class BinarySerializer
    {
        private class ContainerPlaceholder : StackItem
        {
            public override StackItemType Type { get; }
            public int ElementCount { get; }

            public ContainerPlaceholder(StackItemType type, int count)
            {
                Type = type;
                ElementCount = count;
            }

            public override bool Equals(StackItem other) => throw new NotSupportedException();

            public override int GetHashCode() => throw new NotSupportedException();

            public override bool GetBoolean() => throw new NotSupportedException();
        }

        public static StackItem Deserialize(BinaryReader reader, uint maxArraySize, uint maxItemSize, ReferenceCounter? referenceCounter)
        {
            Stack<StackItem> deserialized = new Stack<StackItem>();
            int undeserialized = 1;
            while (undeserialized-- > 0)
            {
                StackItemType type = (StackItemType)reader.ReadByte();
                switch (type)
                {
                    case StackItemType.Any:
                        deserialized.Push(StackItem.Null);
                        break;
                    case StackItemType.Boolean:
                        deserialized.Push(reader.ReadBoolean());
                        break;
                    case StackItemType.Integer:
                        deserialized.Push(new BigInteger(reader.ReadVarBytes(Integer.MaxSize)));
                        break;
                    case StackItemType.ByteString:
                        deserialized.Push(reader.ReadVarBytes((int)maxItemSize));
                        break;
                    case StackItemType.Buffer:
                        Neo.VM.Types.Buffer buffer = new Neo.VM.Types.Buffer((int)reader.ReadVarInt(maxItemSize));
                        reader.FillBuffer(buffer.InnerBuffer);
                        deserialized.Push(buffer);
                        break;
                    case StackItemType.Array:
                    case StackItemType.Struct:
                        {
                            int count = (int)reader.ReadVarInt(maxArraySize);
                            deserialized.Push(new ContainerPlaceholder(type, count));
                            undeserialized += count;
                        }
                        break;
                    case StackItemType.Map:
                        {
                            int count = (int)reader.ReadVarInt(maxArraySize);
                            deserialized.Push(new ContainerPlaceholder(type, count));
                            undeserialized += count * 2;
                        }
                        break;
                    default:
                        throw new FormatException();
                }
            }
            Stack<StackItem> stack_temp = new Stack<StackItem>();
            while (deserialized.Count > 0)
            {
                StackItem item = deserialized.Pop();
                if (item is ContainerPlaceholder placeholder)
                {
                    switch (placeholder.Type)
                    {
                        case StackItemType.Array:
                            Neo.VM.Types.Array array = new Neo.VM.Types.Array(referenceCounter);
                            for (int i = 0; i < placeholder.ElementCount; i++)
                                array.Add(stack_temp.Pop());
                            item = array;
                            break;
                        case StackItemType.Struct:
                            Struct @struct = new Struct(referenceCounter);
                            for (int i = 0; i < placeholder.ElementCount; i++)
                                @struct.Add(stack_temp.Pop());
                            item = @struct;
                            break;
                        case StackItemType.Map:
                            Map map = new Map(referenceCounter);
                            for (int i = 0; i < placeholder.ElementCount; i++)
                            {
                                StackItem key = stack_temp.Pop();
                                StackItem value = stack_temp.Pop();
                                map[(PrimitiveType)key] = value;
                            }
                            item = map;
                            break;
                    }
                }
                stack_temp.Push(item);
            }
            return stack_temp.Peek();
        }

        public static int GetSize(StackItem item, uint maxSize)
        {
            int size = 0;
            List<CompoundType> serialized = new List<CompoundType>();
            Stack<StackItem> unserialized = new Stack<StackItem>();
            unserialized.Push(item);
            while (unserialized.Count > 0)
            {
                item = unserialized.Pop();
                size++;
                switch (item)
                {
                    case Null _:
                        break;
                    case Neo.VM.Types.Boolean _:
                        size += sizeof(bool);
                        break;
                    case Integer _:
                    case ByteString _:
                    case Neo.VM.Types.Buffer _:
                        {
                            var span = item.GetSpan();
                            size += Neo.IO.Helper.GetVarSize(span.Length);
                            size += span.Length;
                        }
                        break;
                    case Neo.VM.Types.Array array:
                        if (serialized.Any(p => ReferenceEquals(p, array)))
                            throw new NotSupportedException();
                        serialized.Add(array);
                        size += Neo.IO.Helper.GetVarSize(array.Count);
                        for (int i = array.Count - 1; i >= 0; i--)
                            unserialized.Push(array[i]);
                        break;
                    case Map map:
                        if (serialized.Any(p => ReferenceEquals(p, map)))
                            throw new NotSupportedException();
                        serialized.Add(map);
                        size += Neo.IO.Helper.GetVarSize(map.Count);
                        foreach (var pair in map.Reverse())
                        {
                            unserialized.Push(pair.Value);
                            unserialized.Push(pair.Key);
                        }
                        break;
                    default:
                        throw new NotSupportedException();
                }
            }

            if (size > maxSize) throw new InvalidOperationException();
            return size;
        }

        public static void Serialize(StackItem item, BinaryWriter writer, uint maxSize)
        {
            List<CompoundType> serialized = new List<CompoundType>();
            Stack<StackItem> unserialized = new Stack<StackItem>();
            unserialized.Push(item);
            while (unserialized.Count > 0)
            {
                item = unserialized.Pop();
                writer.Write((byte)item.Type);
                switch (item)
                {
                    case Null _:
                        break;
                    case Neo.VM.Types.Boolean _:
                        writer.Write(item.GetBoolean());
                        break;
                    case Integer _:
                    case ByteString _:
                    case Neo.VM.Types.Buffer _:
                        writer.WriteVarBytes(item.GetSpan());
                        break;
                    case Neo.VM.Types.Array array:
                        if (serialized.Any(p => ReferenceEquals(p, array)))
                            throw new NotSupportedException();
                        serialized.Add(array);
                        writer.WriteVarInt(array.Count);
                        for (int i = array.Count - 1; i >= 0; i--)
                            unserialized.Push(array[i]);
                        break;
                    case Map map:
                        if (serialized.Any(p => ReferenceEquals(p, map)))
                            throw new NotSupportedException();
                        serialized.Add(map);
                        writer.WriteVarInt(map.Count);
                        foreach (var pair in map.Reverse())
                        {
                            unserialized.Push(pair.Value);
                            unserialized.Push(pair.Key);
                        }
                        break;
                    default:
                        throw new NotSupportedException();
                }
                if (writer.BaseStream.Position > maxSize)
                    throw new InvalidOperationException();
            }
        }
    }
}
