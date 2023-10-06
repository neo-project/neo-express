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
    public class FaultRecord : ITraceDebugRecord
    {
        public const int RecordKey = 4;

        [Key(0)]
        public readonly string Exception;

        public FaultRecord(string exception)
        {
            Exception = exception;
        }

        public static void Write(IBufferWriter<byte> writer, MessagePackSerializerOptions options, string exception)
        {
            var mpWriter = new MessagePackWriter(writer);
            Write(ref mpWriter, options, exception);
            mpWriter.Flush();
        }

        public static void Write(ref MessagePackWriter writer, MessagePackSerializerOptions options, string exception)
        {
            writer.WriteArrayHeader(2);
            writer.WriteInt32(RecordKey);
            writer.WriteArrayHeader(1);
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, exception, options);
        }
    }
}
