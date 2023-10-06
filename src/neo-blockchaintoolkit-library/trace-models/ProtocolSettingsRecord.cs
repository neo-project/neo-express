// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using MessagePack;
using System.Buffers;

namespace Neo.BlockchainToolkit.TraceDebug
{
    [MessagePackObject]
    public class ProtocolSettingsRecord : ITraceDebugRecord
    {
        public const int RecordKey = 7;

        [Key(0)]
        public readonly uint Network;

        [Key(1)]
        public readonly byte AddressVersion;

        public ProtocolSettingsRecord(uint network, byte addressVersion)
        {
            Network = network;
            AddressVersion = addressVersion;
        }

        public static void Write(IBufferWriter<byte> writer, MessagePackSerializerOptions options, uint network, byte addressVersion)
        {
            var mpWriter = new MessagePackWriter(writer);
            Write(ref mpWriter, options, network, addressVersion);
            mpWriter.Flush();
        }

        public static void Write(ref MessagePackWriter writer, MessagePackSerializerOptions options, uint network, byte addressVersion)
        {
            writer.WriteArrayHeader(2);
            writer.WriteInt32(RecordKey);
            writer.WriteArrayHeader(2);
            writer.WriteUInt32(network);
            writer.Write(addressVersion);
        }
    }
}
