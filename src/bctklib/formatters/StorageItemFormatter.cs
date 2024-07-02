// Copyright (C) 2015-2024 The Neo Project.
//
// StorageItemFormatter.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.SmartContract;

namespace MessagePack.Formatters.Neo.BlockchainToolkit
{
    public class StorageItemFormatter : IMessagePackFormatter<StorageItem>
    {
        public readonly static StorageItemFormatter Instance = new StorageItemFormatter();

        public StorageItem Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.NextMessagePackType == MessagePackType.Array)
            {
                var count = reader.ReadArrayHeader();
                if (count != 1)
                    throw new MessagePackSerializationException($"Invalid StorageItem Array Header {count}");
            }

            var value = options.Resolver.GetFormatter<byte[]>()!.Deserialize(ref reader, options);
            return new StorageItem(value);
        }

        public void Serialize(ref MessagePackWriter writer, StorageItem value, MessagePackSerializerOptions options)
        {
            writer.Write(value.Value.Span);
        }
    }
}
