using Neo.IO;
using Neo;
using System.IO;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.VM;
using NeoExpress.Neo3.Node;

namespace NeoExpress.Neo3.Models
{
    class NotificationRecord : ISerializable
    {
        public UInt160 ScriptHash { get; private set; } = null!;
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
            if (notification.ScriptContainer is IInventory inventory)
            {
                InventoryHash = inventory.Hash;
                InventoryType = inventory.InventoryType;
            }
        }

        public int Size => ScriptHash.Size
            + BinarySerializer.GetSize(State, ExecutionEngineLimits.Default.MaxItemSize)
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
            InventoryHash = reader.ReadSerializable<UInt256>();
            InventoryType = (InventoryType)reader.ReadByte();
        }

        public void Serialize(BinaryWriter writer)
        {
            ((ISerializable)ScriptHash).Serialize(writer);
            BinarySerializer.Serialize(State, writer, ExecutionEngineLimits.Default.MaxItemSize);
            ((ISerializable)InventoryHash).Serialize(writer);
            writer.Write((byte)InventoryType);
        }
    }
}

