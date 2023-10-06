// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

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
