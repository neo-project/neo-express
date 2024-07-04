// Copyright (C) 2015-2024 The Neo Project.
//
// TraceDebugResolver.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using MessagePack.Formatters;
using MessagePack.Formatters.Neo.BlockchainToolkit;
using MessagePack.Formatters.Neo.BlockchainToolkit.TraceDebug;

namespace MessagePack.Resolvers
{
    public static class TraceDebugResolver
    {
        public static readonly IFormatterResolver Instance = CompositeResolver.Create(
            new IMessagePackFormatter[]
            {
                ScriptFormatter.Instance,
                StackItemFormatter.Instance,
                StorageItemFormatter.Instance,
                TraceRecordFormatter.Instance,
                UInt160Formatter.Instance
            },
            new IFormatterResolver[]
            {
                StandardResolver.Instance
            });
    }
}
