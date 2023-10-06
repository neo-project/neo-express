// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using MessagePack;
using Neo.VM;
using System.Buffers;

namespace Neo.BlockchainToolkit.TraceDebug
{
    [MessagePackObject]
    public class ScriptRecord : ITraceDebugRecord
    {
        public const int RecordKey = 5;

        [Key(0)]
        public readonly UInt160 ScriptHash;

        [Key(1)]
        public readonly Script Script;

        public ScriptRecord(UInt160 scriptHash, Script script)
        {
            ScriptHash = scriptHash;
            Script = script;
        }

        public static void Write(IBufferWriter<byte> writer, MessagePackSerializerOptions options, Script script)
        {
            var mpWriter = new MessagePackWriter(writer);
            Write(ref mpWriter, options, script);
            mpWriter.Flush();
        }

        public static void Write(ref MessagePackWriter writer, MessagePackSerializerOptions options, Script script)
        {
            var scriptHash = script.CalculateScriptHash();

            writer.WriteArrayHeader(2);
            writer.WriteInt32(RecordKey);
            writer.WriteArrayHeader(2);
            options.Resolver.GetFormatterWithVerify<UInt160>().Serialize(ref writer, scriptHash, options);
            options.Resolver.GetFormatterWithVerify<Script>().Serialize(ref writer, script, options);
            writer.Flush();
        }
    }
}
