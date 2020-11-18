using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using MessagePack;
using MessagePack.Resolvers;
using Neo;
using Neo.BlockchainToolkit.TraceDebug;
using Neo.Ledger;
using Neo.SmartContract;
using Neo.VM;
using Nerdbank.Streams;
using StackItem = Neo.VM.Types.StackItem;

namespace NeoExpress.Neo3.Node
{
    // The trace methods in this class are designed to write out MessagePack messages
    // that can be deserialized into types defined in the Neo.BlockchainToolkit.TraceDebug
    // package. However, this code replicates the MessagePack serialization logic
    // in order to avoid having to create garbage during serialization.

    internal class TraceDebugSink : ITraceDebugSink
    {
        private readonly static MessagePackSerializerOptions options;

        static TraceDebugSink()
        {
            options = MessagePackSerializerOptions.Standard
                .WithResolver(TraceDebugResolver.Instance);
        }

        readonly Sequence<byte> sequence = new Sequence<byte>();

        public TraceDebugSink()
        {
        }

        public void Dispose()
        {
        }

        public void Write(Stream stream)
        {
            foreach (var segment in sequence.AsReadOnlySequence)
            {
                stream.Write(segment.Span);
            }
        }

        public void Trace(VMState vmState, IReadOnlyCollection<ExecutionContext> executionContexts)
        {
            TraceRecord.Write(sequence, options, vmState, executionContexts);
        }

        public void Notify(NotifyEventArgs args)
        {
            NotifyRecord.Write(sequence, options, args.ScriptHash, args.EventName, args.State);
        }

        public void Log(LogEventArgs args)
        {
            LogRecord.Write(sequence, options, args.ScriptHash, args.Message);
        }

        public void Results(VMState vmState, long gasConsumed, IReadOnlyCollection<StackItem> results)
        {
            ResultsRecord.Write(sequence, options, vmState, gasConsumed, results);
        }

        public void Fault(Exception exception)
        {
            FaultRecord.Write(sequence, options, exception.Message);
        }

        public void Script(byte[] script)
        {
            ScriptRecord.Write(sequence, options, script);
        }

        public void Storages(UInt160 scriptHash, IEnumerable<(StorageKey key, StorageItem item)> storages)
        {
            StorageRecord.Write(sequence, options, scriptHash, storages);
        }
    }
}
