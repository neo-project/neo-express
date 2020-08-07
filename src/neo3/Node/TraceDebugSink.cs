using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MessagePack;
using MessagePack.Resolvers;
using Neo;
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

        public TraceDebugSink(Stream stream)
        {
            this.stream = stream;
        }

        public void Dispose()
        {
            stream.Flush();
            stream.Dispose();
        }

        public void Trace(VMState vmState, IReadOnlyCollection<ExecutionContext> stackFrames, IEnumerable<(UInt160 scriptHash, byte[] key, StorageItem item)> storages)
        {
            var pipe = stream.UseStrictPipeWriter();
            var writer = new MessagePackWriter(pipe);
            writer.WriteArrayHeader(2);
            writer.WriteInt32(0);
            writer.WriteArrayHeader(3);
            options.Resolver.GetFormatterWithVerify<VMState>().Serialize(ref writer, vmState, options);

            writer.WriteArrayHeader(stackFrames.Count);
            foreach (var context in stackFrames)
            {
                writer.WriteArrayHeader(6);
                options.Resolver.GetFormatterWithVerify<UInt160>().Serialize(ref writer, context.GetScriptHash(), options);
                writer.Write(context.InstructionPointer);
                options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<StackItem>>().Serialize(ref writer, context.EvaluationStack, options);
                options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<StackItem>>().Serialize(ref writer, context.LocalVariables, options);
                options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<StackItem>>().Serialize(ref writer, context.StaticFields, options);
                options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<StackItem>>().Serialize(ref writer, context.Arguments, options);
            }

            var storageGroups = storages.GroupBy(t => t.scriptHash);
            writer.WriteMapHeader(storageGroups.Count());
            foreach (var group in storageGroups)
            {
                options.Resolver.GetFormatterWithVerify<UInt160>().Serialize(ref writer, group.Key, options);
                writer.WriteMapHeader(group.Count());
                foreach (var (_, key, item) in group)
                {
                    writer.Write(key);
                    options.Resolver.GetFormatterWithVerify<StorageItem>().Serialize(ref writer, item, options);
                }
            }
            writer.Flush();
            pipe.Complete();
        }

        public void Notify(NotifyEventArgs args)
        {
            var pipe = stream.UseStrictPipeWriter();
            var writer = new MessagePackWriter(pipe);
            writer.WriteArrayHeader(2);
            writer.WriteInt32(1);
            writer.WriteArrayHeader(3);
            options.Resolver.GetFormatterWithVerify<UInt160>().Serialize(ref writer, args.ScriptHash, options);
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, args.EventName, options);
            options.Resolver.GetFormatterWithVerify<IReadOnlyList<StackItem>>().Serialize(ref writer, args.State, options);
            writer.Flush();
            pipe.Complete();
        }

        public void Log(LogEventArgs args)
        {
            var pipe = stream.UseStrictPipeWriter();
            var writer = new MessagePackWriter(pipe);
            writer.WriteArrayHeader(2);
            writer.WriteInt32(2);
            writer.WriteArrayHeader(2);
            options.Resolver.GetFormatterWithVerify<UInt160>().Serialize(ref writer, args.ScriptHash, options);
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, args.Message, options);
            writer.Flush();
            pipe.Complete();
        }

        public void Results(VMState vmState, long gasConsumed, IReadOnlyCollection<StackItem> results)
        {
            var pipe = stream.UseStrictPipeWriter();
            var writer = new MessagePackWriter(pipe);
            writer.WriteArrayHeader(2);
            writer.WriteInt32(3);
            writer.WriteArrayHeader(3);
            options.Resolver.GetFormatterWithVerify<VMState>().Serialize(ref writer, vmState, options);
            writer.Write(gasConsumed);
            options.Resolver.GetFormatterWithVerify<IReadOnlyCollection<StackItem>>().Serialize(ref writer, results, options);
            writer.Flush();
            pipe.Complete();
        }

        public void Fault(Exception exception)
        {
            var pipe = stream.UseStrictPipeWriter();
            var writer = new MessagePackWriter(pipe);
            writer.WriteArrayHeader(2);
            writer.WriteInt32(4);
            writer.WriteArrayHeader(1);
            options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, exception.Message, options);
            writer.Flush();
            pipe.Complete();
        }
    }
}
