// Copyright (C) 2015-2024 The Neo Project.
//
// UInt160Formatter.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.IO;
using System.Buffers;

namespace MessagePack.Formatters.Neo.BlockchainToolkit
{
    public class UInt160Formatter : IMessagePackFormatter<UInt160>
    {
        public static readonly UInt160Formatter Instance = new UInt160Formatter();

        public UInt160 Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            // post RC3 serialization format of UInt160
            if (reader.NextMessagePackType == MessagePackType.Binary)
            {
                var value = options.Resolver.GetFormatter<byte[]>()!.Deserialize(ref reader, options);
                return new UInt160(value);
            }

            // pre RC3 serialization format of UInt160
            if (reader.NextMessagePackType == MessagePackType.Integer)
            {
                var seq = reader.ReadRaw(UInt160.Length);
                return new UInt160(seq.IsSingleSegment ? seq.FirstSpan : seq.ToArray());
            }

            throw new MessagePackSerializationException($"Unexpected UInt160 MessagePack type {reader.NextMessagePackType}");
        }

        public void Serialize(ref MessagePackWriter writer, UInt160 value, MessagePackSerializerOptions options)
        {
            writer.Write(value.ToArray());
        }
    }
}
