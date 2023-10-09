// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using MessagePack;
using System.Buffers;

namespace Neo.BlockchainToolkit.TraceDebug
{
    [MessagePackObject]
    public class LogRecord : ITraceDebugRecord
    {
        public const int RecordKey = 2;

        [Key(0)]
        public readonly UInt160 ScriptHash;
        [Key(1)]
        public readonly string ScriptName;
        [Key(2)]
        public readonly string Message;

        public LogRecord(UInt160 scriptHash, string scriptName, string message)
        {
            ScriptHash = scriptHash;
            ScriptName = scriptName;
            Message = message;
        }

        public static void Write(IBufferWriter<byte> writer, MessagePackSerializerOptions options, UInt160 scriptHash, string scriptName, string message)
        {
            var mpWriter = new MessagePackWriter(writer);
            Write(ref mpWriter, options, scriptHash, scriptName, message);
            mpWriter.Flush();
        }

        public static void Write(ref MessagePackWriter writer, MessagePackSerializerOptions options, UInt160 scriptHash, string scriptName, string message)
        {
            var stringFormatter = options.Resolver.GetFormatterWithVerify<string>();

            writer.WriteArrayHeader(2);
            writer.WriteInt32(RecordKey);
            writer.WriteArrayHeader(3);
            options.Resolver.GetFormatterWithVerify<Neo.UInt160>().Serialize(ref writer, scriptHash, options);
            stringFormatter.Serialize(ref writer, scriptName, options);
            stringFormatter.Serialize(ref writer, message, options);
            writer.Flush();
        }
    }
}
