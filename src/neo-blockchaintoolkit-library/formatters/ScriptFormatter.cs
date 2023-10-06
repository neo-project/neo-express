// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Neo.BlockchainToolkit;
using Neo.VM;
using System;

namespace MessagePack.Formatters.Neo.BlockchainToolkit
{
    public class ScriptFormatter : IMessagePackFormatter<Script>
    {
        public static readonly ScriptFormatter Instance = new ScriptFormatter();

        public Script Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var bytes = options.Resolver.GetFormatter<byte[]>().Deserialize(ref reader, options);
            return new Script(bytes);
        }

        public void Serialize(ref MessagePackWriter writer, Script value, MessagePackSerializerOptions options)
        {
            writer.Write(value.AsSpan());
        }
    }
}
