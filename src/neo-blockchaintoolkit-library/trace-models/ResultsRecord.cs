// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using MessagePack;
using Neo.VM;
using System.Buffers;
using System.Collections.Generic;
using StackItem = Neo.VM.Types.StackItem;

namespace Neo.BlockchainToolkit.TraceDebug
{
    [MessagePackObject]
    public class ResultsRecord : ITraceDebugRecord
    {
        public const int RecordKey = 3;

        [Key(0)]
        public readonly VMState State;
        [Key(1)]
        public readonly long GasConsumed;
        [Key(2)]
        public readonly IReadOnlyList<StackItem> ResultStack;

        public ResultsRecord(VMState vmState, long gasConsumed, IReadOnlyList<StackItem> resultStack)
        {
            State = vmState;
            GasConsumed = gasConsumed;
            ResultStack = resultStack;
        }

        public static void Write(IBufferWriter<byte> writer, MessagePackSerializerOptions options, VMState vmState, long gasConsumed, IReadOnlyCollection<StackItem> resultStack)
        {
            var mpWriter = new MessagePackWriter(writer);
            Write(ref mpWriter, options, vmState, gasConsumed, resultStack);
            mpWriter.Flush();
        }

        public static void Write(ref MessagePackWriter writer, MessagePackSerializerOptions options, VMState vmState, long gasConsumed, IReadOnlyCollection<StackItem> resultStack)
        {
            writer.WriteArrayHeader(2);
            writer.WriteInt32(RecordKey);
            writer.WriteArrayHeader(3);
            options.Resolver.GetFormatterWithVerify<VMState>().Serialize(ref writer, vmState, options);
            writer.Write(gasConsumed);
            options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<StackItem>>().Serialize(ref writer, resultStack, options);
        }
    }
}
