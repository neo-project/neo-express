using Neo.IO;
using Neo;
using System.IO;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.VM;
using NeoExpress.Neo3.Node;
using System.Collections.Generic;
using System;
using System.Linq;

namespace NeoExpress.Neo3.Models
{
    class NotificationRecord : ISerializable
    {
        public UInt160 ScriptHash { get; private set; } = null!;
        public string EventName { get; private set; } = null!;
        public Neo.VM.Types.Array State { get; private set; } = null!;
        public UInt256 InventoryHash { get; private set; } = UInt256.Zero;
        public InventoryType InventoryType { get; private set; }

        public NotificationRecord()
        {
        }

        public NotificationRecord(NotifyEventArgs notification)
        {
            ScriptHash = notification.ScriptHash;
            State = notification.State;
            EventName = notification.EventName;
            if (notification.ScriptContainer is IInventory inventory)
            {
                InventoryHash = inventory.Hash;
                InventoryType = inventory.InventoryType;
            }
        }

        public int Size => ScriptHash.Size
            + GetSize(State, ExecutionEngineLimits.Default.MaxItemSize)
            + EventName.GetVarSize()
            + InventoryHash.Size
            + sizeof(byte);

        public void Deserialize(BinaryReader reader)
        {
            ScriptHash = reader.ReadSerializable<UInt160>();
            State = (Neo.VM.Types.Array)BinarySerializer.Deserialize(
                reader,
                ExecutionEngineLimits.Default.MaxStackSize,
                ExecutionEngineLimits.Default.MaxItemSize,
                null);
            EventName = reader.ReadVarString();
            InventoryHash = reader.ReadSerializable<UInt256>();
            InventoryType = (InventoryType)reader.ReadByte();
        }

        public void Serialize(BinaryWriter writer)
        {
            ((ISerializable)ScriptHash).Serialize(writer);
            BinarySerializer.Serialize(State, writer, ExecutionEngineLimits.Default.MaxItemSize);
            writer.WriteVarString(EventName);
            ((ISerializable)InventoryHash).Serialize(writer);
            writer.Write((byte)InventoryType);
        }

        static int GetSize(Neo.VM.Types.StackItem item, uint maxSize)
        {
            int size = 0;
            var serialized = new List<Neo.VM.Types.CompoundType>();
            var unserialized = new Stack<Neo.VM.Types.StackItem>();
            unserialized.Push(item);
            while (unserialized.Count > 0)
            {
                item = unserialized.Pop();
                size++;
                switch (item)
                {
                    case Neo.VM.Types.Null _:
                        break;
                    case Neo.VM.Types.Boolean _:
                        size += sizeof(bool);
                        break;
                    case Neo.VM.Types.Integer _:
                    case Neo.VM.Types.ByteString _:
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
                    case Neo.VM.Types.Map map:
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
    }
}

