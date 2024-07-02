// Copyright (C) 2015-2024 The Neo Project.
//
// FaultRecord.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

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
