using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Neo;
using Neo.Ledger;
using Neo.SmartContract;
using Neo.VM;
using Newtonsoft.Json;

namespace NeoExpress.Neo3.Node
{
    using StackItem = Neo.VM.Types.StackItem;

    internal class TraceDebugJsonSink : ITraceDebugSink
    {
        private readonly JsonWriter writer;

        public TraceDebugJsonSink(string path)
        {
            var fileStream = File.Open(path, FileMode.Create, FileAccess.Write);
            var textWriter = new StreamWriter(fileStream);
            writer = new JsonTextWriter(textWriter)
            {
                Formatting = Formatting.Indented,
            };

            writer.WriteStartArray();
        }

        public void Dispose()
        {
            writer.Close();
        }

        public void Trace(VMState vmState, IReadOnlyCollection<ExecutionContext> stackFrames, IEnumerable<(UInt160 scriptHash, byte[] key, StorageItem item)> storages)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("type");
            writer.WriteValue("trace");
            writer.WritePropertyName("vmstate");
            writer.WriteValue(vmState.ToString());

            writer.WritePropertyName("stack-frames");
            writer.WriteStartArray();
            foreach (var stackFrame in stackFrames)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("script-hash");
                writer.WriteValue(stackFrame.GetScriptHash().ToString());
                writer.WritePropertyName("instruction-pointer");
                writer.WriteValue(stackFrame.InstructionPointer);
                writer.WritePropertyName("eval-stack");
                WriteStack(writer, stackFrame.EvaluationStack);
                writer.WritePropertyName("locals");
                WriteStack(writer, stackFrame.LocalVariables);
                writer.WritePropertyName("statics");
                WriteStack(writer, stackFrame.StaticFields);
                writer.WritePropertyName("args");
                WriteStack(writer, stackFrame.Arguments);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WritePropertyName("storages");
            writer.WriteStartObject();
            foreach (var g in storages.GroupBy(t => t.scriptHash))
            {
                writer.WritePropertyName("script-hash");
                writer.WriteValue(g.Key.ToString());
                writer.WritePropertyName("values");
                writer.WriteStartArray();
                foreach (var (_, key, item) in g)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("key");
                    writer.WriteValue(key);
                    writer.WritePropertyName("value");
                    writer.WriteValue(item.Value);
                    writer.WritePropertyName("constant");
                    writer.WriteValue(item.IsConstant);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        public void Fault(Exception exception)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("type");
            writer.WriteValue("fault");
            writer.WritePropertyName("exception");
            writer.WriteValue(exception.Message);
            writer.WriteEndObject();
        }

        public void Log(LogEventArgs args)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("type");
            writer.WriteValue("log");
            writer.WritePropertyName("script-hash");
            writer.WriteValue(args.ScriptHash.ToString());
            writer.WritePropertyName("message");
            writer.WriteValue(args.Message);
            writer.WriteEndObject();
        }

        public void Notify(NotifyEventArgs args)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("type");
            writer.WriteValue("notification");
            writer.WritePropertyName("script-hash");
            writer.WriteValue(args.ScriptHash.ToString());
            writer.WritePropertyName("state");
            WriteStackItem(writer, args.State);
            writer.WriteEndObject();
        }

        public void Results(VMState vmState, long gasConsumed, IReadOnlyCollection<StackItem> results)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("type");
            writer.WriteValue("results");
            writer.WritePropertyName("vmstate");
            writer.WriteValue(vmState.ToString());
            writer.WritePropertyName("gas-consumed");
            writer.WriteValue(gasConsumed.ToString());
            writer.WritePropertyName("results");
            WriteStack(writer, results);
            writer.WriteEndObject();
        }

        private static void WriteStack(JsonWriter writer, IReadOnlyCollection<StackItem> stack)
        {
            writer.WriteStartArray();
            foreach (var result in stack)
            {
                WriteStackItem(writer, result);
            }
            writer.WriteEndArray();
        }

        private static void WriteStackItem(JsonWriter writer, StackItem item)
        {
            void WriteByteArray(ReadOnlySpan<byte> span)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("type");
                writer.WriteValue("byte-array");
                writer.WritePropertyName("value");
                writer.WriteValue(Convert.ToBase64String(span));
                writer.WriteEndObject();
            }
            switch (item)
            {
                case Neo.VM.Types.InteropInterface _:
                    writer.WriteStartObject();
                    writer.WritePropertyName("type");
                    writer.WriteValue("interop-interface");
                    writer.WriteEndObject();
                    break;
                case Neo.VM.Types.Boolean _:
                    writer.WriteValue(item.GetBoolean());
                    break;
                case Neo.VM.Types.Buffer buffer:
                    WriteByteArray(buffer.InnerBuffer);
                    break;
                case Neo.VM.Types.ByteString byteString:
                    WriteByteArray(byteString);
                    break;
                case Neo.VM.Types.Integer @int:
                    writer.WriteStartObject();
                    writer.WritePropertyName("type");
                    writer.WriteValue("integer");
                    writer.WritePropertyName("value");
                    writer.WriteValue($"{@int.GetInteger()}");
                    writer.WriteEndObject();
                    break;
                case Neo.VM.Types.Null _:
                    writer.WriteNull();
                    break;
                case Neo.VM.Types.Pointer pointer:
                    writer.WriteStartObject();
                    writer.WritePropertyName("type");
                    writer.WriteValue("pointer");
                    writer.WriteEndObject();
                    break;
                case Neo.VM.Types.Map map:
                    writer.WriteStartArray();
                    foreach (var kvp in map)
                    {
                        writer.WritePropertyName("key");
                        WriteStackItem(writer, kvp.Key);
                        writer.WritePropertyName("value");
                        WriteStackItem(writer, kvp.Value);
                    }
                    writer.WriteEndArray();
                    break;
                case Neo.VM.Types.Array array:
                    writer.WriteStartArray();
                    foreach (var arrayItem in array)
                    {
                        WriteStackItem(writer, arrayItem);
                    }
                    writer.WriteEndArray();
                    break;
            }
        }
    }
}
