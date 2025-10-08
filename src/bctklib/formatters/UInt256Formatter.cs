// Copyright (C) 2015-2025 The Neo Project.
//
// UInt256Formatter.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.IO;
using System.Buffers;

namespace MessagePack.Formatters.Neo.BlockchainToolkit
{
    public class UInt256Formatter : IMessagePackFormatter<UInt256?>
    {
        public static readonly UInt256Formatter Instance = new UInt256Formatter();

        public UInt256 Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var value = options.Resolver.GetFormatter<byte[]>()!.Deserialize(ref reader, options);
            return new UInt256(value);
        }

        public void Serialize(ref MessagePackWriter writer, UInt256? value, MessagePackSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(value, nameof(value));

            var buffer = new byte[UInt256.Length];
            value.Serialize(buffer);
            writer.Write(buffer);
        }
    }
}
