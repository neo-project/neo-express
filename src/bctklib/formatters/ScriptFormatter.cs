// Copyright (C) 2015-2024 The Neo Project.
//
// ScriptFormatter.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BlockchainToolkit;
using Neo.VM;

namespace MessagePack.Formatters.Neo.BlockchainToolkit
{
    public class ScriptFormatter : IMessagePackFormatter<Script>
    {
        public static readonly ScriptFormatter Instance = new ScriptFormatter();

        public Script Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var bytes = options.Resolver.GetFormatter<byte[]>()!.Deserialize(ref reader, options);
            return new Script(bytes);
        }

        public void Serialize(ref MessagePackWriter writer, Script value, MessagePackSerializerOptions options)
        {
            writer.Write(value.AsSpan());
        }
    }
}
