// Copyright (C) 2015-2024 The Neo Project.
//
// TraceDebugStream.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using MessagePack;
using MessagePack.Resolvers;
using Neo.SmartContract;
using Neo.VM;
using Nerdbank.Streams;
using System.Buffers;
using ExecutionContext = Neo.VM.ExecutionContext;
using StackItem = Neo.VM.Types.StackItem;

namespace Neo.BlockchainToolkit.TraceDebug
{
    // The trace methods in this class are designed to write out MessagePack messages
    // that can be deserialized into types defined in the Neo.BlockchainToolkit.TraceDebug
    // package. However, this code replicates the MessagePack serialization logic
    // in order to avoid having to create garbage during serialization.

    public class TraceDebugStream : ITraceDebugSink
    {
        private readonly static MessagePackSerializerOptions options;
        private readonly IDictionary<UInt160, UInt160> scriptIdMap = new Dictionary<UInt160, UInt160>();

        static TraceDebugStream()
        {
            options = MessagePackSerializerOptions.Standard
                .WithResolver(TraceDebugResolver.Instance);
        }

        private readonly Stream stream;
        private readonly Sequence<byte> sequence = new Sequence<byte>();

        public TraceDebugStream(Stream stream)
        {
            this.stream = stream;
        }

        public void Dispose()
        {
            stream.Flush();
            stream.Dispose();
            GC.SuppressFinalize(this);
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

        public void Trace(VMState vmState, long gasConsumed, IReadOnlyCollection<ExecutionContext> executionContexts)
        {
            Write((seq, opt) => TraceRecord.Write(seq, opt, vmState, gasConsumed, executionContexts, GetScriptIdentifier));
        }

        UInt160 GetScriptIdentifier(ExecutionContext context)
        {
            var scriptHash = context.GetScriptHash();
            if (scriptIdMap.TryGetValue(scriptHash, out var scriptId))
            {
                return scriptId;
            }

            scriptId = context.Script.CalculateScriptHash();
            scriptIdMap[scriptHash] = scriptId;
            return scriptId;
        }

        public void Notify(NotifyEventArgs args, string scriptName)
        {
            Write((seq, opt) => NotifyRecord.Write(seq, opt, args.ScriptHash, scriptName, args.EventName, args.State));
        }

        public void Log(LogEventArgs args, string scriptName)
        {
            Write((seq, opt) => LogRecord.Write(seq, opt, args.ScriptHash, scriptName, args.Message));
        }

        public void Results(VMState vmState, long gasConsumed, IReadOnlyCollection<StackItem> results)
        {
            Write((seq, opt) => ResultsRecord.Write(seq, opt, vmState, gasConsumed, results));
        }

        public void Fault(Exception exception)
        {
            Write((seq, opt) => FaultRecord.Write(seq, opt, exception.Message));
        }

        public void Script(Script script)
        {
            Write((seq, opt) => ScriptRecord.Write(seq, opt, script));
        }

        public void Storages(UInt160 scriptHash, IEnumerable<(StorageKey key, StorageItem item)> storages)
        {
            Write((seq, opt) => StorageRecord.Write(seq, opt, scriptHash, storages));
        }

        public void ProtocolSettings(uint network, byte addressVersion)
        {
            Write((seq, opt) => ProtocolSettingsRecord.Write(seq, opt, network, addressVersion));
        }
    }
}
