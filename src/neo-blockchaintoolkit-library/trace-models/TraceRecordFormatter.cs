// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Neo.BlockchainToolkit.TraceDebug;
using System.Collections.Generic;
using VMState = Neo.VM.VMState;

namespace MessagePack.Formatters.Neo.BlockchainToolkit.TraceDebug
{
    public class TraceRecordFormatter : IMessagePackFormatter<TraceRecord>
    {
        public static readonly TraceRecordFormatter Instance = new TraceRecordFormatter();

        public TraceRecord Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            // Older trace records (N3 RC3 and before) did not have gas consumed value.
            // When parsing TraceRecords, if there are only two fields in the TraceRecord array, provide a dummy gasConsumed value.

            var fieldCount = reader.ReadArrayHeader();
            if (fieldCount != 2 && fieldCount != 3)
                throw new MessagePackSerializationException($"Invalid TraceRecord Array Header {fieldCount}");

            var state = options.Resolver.GetFormatterWithVerify<VMState>().Deserialize(ref reader, options);
            var gasConsumed = fieldCount == 3 ? reader.ReadInt64() : 0;
            var stackFrames = options.Resolver.GetFormatterWithVerify<IReadOnlyList<TraceRecord.StackFrame>>().Deserialize(ref reader, options);

            return new TraceRecord(state, gasConsumed, stackFrames);
        }

        public void Serialize(ref MessagePackWriter writer, TraceRecord value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(3);
            options.Resolver.GetFormatterWithVerify<VMState>().Serialize(ref writer, value.State, options);
            writer.WriteInt64(value.GasConsumed);
            options.Resolver.GetFormatterWithVerify<IReadOnlyList<TraceRecord.StackFrame>>().Serialize(ref writer, value.StackFrames, options);
        }
    }
}
