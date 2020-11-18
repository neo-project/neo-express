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

        private readonly Stream stream;
        private readonly Sequence<byte> sequence = new Sequence<byte>();

        public TraceDebugSink(Stream stream)
        {
            this.stream = stream;
        }

        public void Dispose()
        {
            stream.Flush();
            stream.Dispose();
        }

        private void Write(Action<IBufferWriter<byte>, MessagePackSerializerOptions> funcWrite)
        {
            try
            {
                sequence.Reset();
                funcWrite(sequence, options);
                foreach (var segment in sequence.AsReadOnlySequence)
                {
                    stream.Write(segment.Span);
                }
            }
            catch
            {
            }
        }

        public void Trace(VMState vmState, IReadOnlyCollection<ExecutionContext> executionContexts)
        {
            Write((seq, opt) => TraceRecord.Write(seq, opt, vmState, executionContexts));
        }

        public void Notify(NotifyEventArgs args)
        {
            Write((seq, opt) => NotifyRecord.Write(seq, opt, args.ScriptHash, args.EventName, args.State));
        }

        public void Log(LogEventArgs args)
        {
            Write((seq, opt) => LogRecord.Write(seq, opt, args.ScriptHash, args.Message));
        }

        public void Results(VMState vmState, long gasConsumed, IReadOnlyCollection<StackItem> results)
        {
            Write((seq, opt) => ResultsRecord.Write(seq, opt, vmState, gasConsumed, results));
        }

        public void Fault(Exception exception)
        {
            Write((seq, opt) => FaultRecord.Write(seq, opt, exception.Message));
        }

        public void Script(byte[] script)
        {
            Write((seq, opt) => ScriptRecord.Write(seq, opt, script));
        }

        public void Storages(UInt160 scriptHash, IEnumerable<(StorageKey key, StorageItem item)> storages)
        {
            Write((seq, opt) => StorageRecord.Write(seq, opt, scriptHash, storages));
        }
    }
}
