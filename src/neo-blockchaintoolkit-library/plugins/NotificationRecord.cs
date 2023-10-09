// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.VM;
using System.IO;
using NeoArray = Neo.VM.Types.Array;

namespace Neo.BlockchainToolkit.Plugins
{
    public class NotificationRecord : ISerializable
    {
        public UInt160 ScriptHash { get; private set; } = null!;
        public string EventName { get; private set; } = null!;
        public NeoArray State { get; private set; } = null!;
        public InventoryType InventoryType { get; private set; }
        public UInt256 InventoryHash { get; private set; } = UInt256.Zero;

        public NotificationRecord() { }

        public NotificationRecord(NotifyEventArgs notification)
        {
            ScriptHash = notification.ScriptHash;
            State = notification.State;
            EventName = notification.EventName;
            if (notification.ScriptContainer is IInventory inventory)
            {
                InventoryType = inventory.InventoryType;
                InventoryHash = inventory.Hash;
            }
        }

        public NotificationRecord(UInt160 scriptHash, string eventName, NeoArray state, InventoryType inventoryType, UInt256 inventoryHash)
        {
            ScriptHash = scriptHash;
            EventName = eventName;
            State = state;
            InventoryType = inventoryType;
            InventoryHash = inventoryHash;
        }

        public int Size => UInt160.Length
            + State.GetSize(ExecutionEngineLimits.Default.MaxItemSize)
            + EventName.GetVarSize()
            + UInt256.Length
            + sizeof(byte);

        public void Deserialize(ref MemoryReader reader)
        {
            ScriptHash = reader.ReadSerializable<UInt160>();
            State = (NeoArray)BinarySerializer.Deserialize(ref reader, ExecutionEngineLimits.Default, null);
            EventName = reader.ReadVarString();
            InventoryHash = reader.ReadSerializable<UInt256>();
            InventoryType = (InventoryType)reader.ReadByte();
        }

        public void Serialize(BinaryWriter writer)
        {
            ScriptHash.Serialize(writer);
            BinarySerializer.Serialize(writer, State, ExecutionEngineLimits.Default.MaxItemSize);
            writer.WriteVarString(EventName);
            InventoryHash.Serialize(writer);
            writer.Write((byte)InventoryType);
        }
    }
}

