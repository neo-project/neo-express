// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

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

            var value = options.Resolver.GetFormatter<byte[]>().Deserialize(ref reader, options);
            return new StorageItem(value);
        }

        public void Serialize(ref MessagePackWriter writer, StorageItem value, MessagePackSerializerOptions options)
        {
            writer.Write(value.Value.Span);
        }
    }
}
