// Copyright (C) 2015-2024 The Neo Project.
//
// NotifyRecord.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using MessagePack;
using System.Buffers;
using StackItem = Neo.VM.Types.StackItem;

namespace Neo.BlockchainToolkit.TraceDebug
{
    [MessagePackObject]
    public class NotifyRecord : ITraceDebugRecord
    {
        public const int RecordKey = 1;

        [Key(0)]
        public readonly UInt160 ScriptHash;
        [Key(1)]
        public readonly string ScriptName;
        [Key(2)]
        public readonly string EventName;
        [Key(3)]
        public readonly IReadOnlyList<StackItem> State;

        public NotifyRecord(UInt160 scriptHash, string scriptName, string eventName, IReadOnlyList<StackItem> state)
        {
            ScriptHash = scriptHash;
            ScriptName = scriptName;
            EventName = eventName;
            State = state;
        }

        public static void Write(IBufferWriter<byte> writer, MessagePackSerializerOptions options, UInt160 scriptHash, string scriptName, string eventName, IReadOnlyCollection<StackItem> state)
        {
            var mpWriter = new MessagePackWriter(writer);
            mpWriter.WriteArrayHeader(2);
            mpWriter.WriteInt32(RecordKey);
            mpWriter.WriteArrayHeader(4);
            options.Resolver.GetFormatterWithVerify<UInt160>().Serialize(ref mpWriter, scriptHash, options);
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref mpWriter, scriptName, options);
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref mpWriter, eventName, options);
            mpWriter.WriteArrayHeader(state.Count);
            foreach (var item in state)
            {
                options.Resolver.GetFormatterWithVerify<StackItem>().Serialize(ref mpWriter, item, options);
            }
            mpWriter.Flush();
        }

        public static void Write(ref MessagePackWriter writer, MessagePackSerializerOptions options, UInt160 scriptHash, string scriptName, string eventName, IReadOnlyCollection<StackItem> state)
        {
            var stackItemFormatter = options.Resolver.GetFormatterWithVerify<StackItem>();
            var stringFormatter = options.Resolver.GetFormatterWithVerify<string>();

            writer.WriteArrayHeader(2);
            writer.WriteInt32(RecordKey);
            writer.WriteArrayHeader(4);
            options.Resolver.GetFormatterWithVerify<UInt160>().Serialize(ref writer, scriptHash, options);
            stringFormatter.Serialize(ref writer, scriptName, options);
            stringFormatter.Serialize(ref writer, eventName, options);
            writer.WriteArrayHeader(state.Count);
            foreach (var item in state)
            {
                stackItemFormatter.Serialize(ref writer, item, options);
            }
            writer.Flush();
        }
    }
}
