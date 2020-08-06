#pragma warning disable 618
#pragma warning disable 612
#pragma warning disable 414
#pragma warning disable 168

#pragma warning disable SA1200 // Using directives should be placed correctly
#pragma warning disable SA1403 // File may only contain a single namespace
#pragma warning disable SA1649 // File name should match first type name

namespace Neo.Seattle.TraceDebug.Formatters
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using MessagePack;

    public sealed class ITraceRecordFormatter : global::MessagePack.Formatters.IMessagePackFormatter<global::Neo.Seattle.TraceDebug.Models.ITraceRecord>
    {
        private readonly Dictionary<RuntimeTypeHandle, KeyValuePair<int, int>> typeToKeyAndJumpMap;
        private readonly Dictionary<int, int> keyToJumpMap;

        public ITraceRecordFormatter()
        {
            this.typeToKeyAndJumpMap = new Dictionary<RuntimeTypeHandle, KeyValuePair<int, int>>(5, global::MessagePack.Internal.RuntimeTypeHandleEqualityComparer.Default)
            {
                { typeof(global::Neo.Seattle.TraceDebug.Models.TracePoint).TypeHandle, new KeyValuePair<int, int>(0, 0) },
                { typeof(global::Neo.Seattle.TraceDebug.Models.Notify).TypeHandle, new KeyValuePair<int, int>(1, 1) },
                { typeof(global::Neo.Seattle.TraceDebug.Models.Log).TypeHandle, new KeyValuePair<int, int>(2, 2) },
                { typeof(global::Neo.Seattle.TraceDebug.Models.Results).TypeHandle, new KeyValuePair<int, int>(3, 3) },
                { typeof(global::Neo.Seattle.TraceDebug.Models.Fault).TypeHandle, new KeyValuePair<int, int>(4, 4) },
            };
            this.keyToJumpMap = new Dictionary<int, int>(5)
            {
                { 0, 0 },
                { 1, 1 },
                { 2, 2 },
                { 3, 3 },
                { 4, 4 },
            };
        }

        public void Serialize(ref MessagePackWriter writer, global::Neo.Seattle.TraceDebug.Models.ITraceRecord value, global::MessagePack.MessagePackSerializerOptions options)
        {
            KeyValuePair<int, int> keyValuePair;
            if (value != null && this.typeToKeyAndJumpMap.TryGetValue(value.GetType().TypeHandle, out keyValuePair))
            {
                writer.WriteArrayHeader(2);
                writer.WriteInt32(keyValuePair.Key);
                switch (keyValuePair.Value)
                {
                    case 0:
                        options.Resolver.GetFormatterWithVerify<global::Neo.Seattle.TraceDebug.Models.TracePoint>().Serialize(ref writer, (global::Neo.Seattle.TraceDebug.Models.TracePoint)value, options);
                        break;
                    case 1:
                        options.Resolver.GetFormatterWithVerify<global::Neo.Seattle.TraceDebug.Models.Notify>().Serialize(ref writer, (global::Neo.Seattle.TraceDebug.Models.Notify)value, options);
                        break;
                    case 2:
                        options.Resolver.GetFormatterWithVerify<global::Neo.Seattle.TraceDebug.Models.Log>().Serialize(ref writer, (global::Neo.Seattle.TraceDebug.Models.Log)value, options);
                        break;
                    case 3:
                        options.Resolver.GetFormatterWithVerify<global::Neo.Seattle.TraceDebug.Models.Results>().Serialize(ref writer, (global::Neo.Seattle.TraceDebug.Models.Results)value, options);
                        break;
                    case 4:
                        options.Resolver.GetFormatterWithVerify<global::Neo.Seattle.TraceDebug.Models.Fault>().Serialize(ref writer, (global::Neo.Seattle.TraceDebug.Models.Fault)value, options);
                        break;
                    default:
                        break;
                }

                return;
            }

            writer.WriteNil();
        }

        public global::Neo.Seattle.TraceDebug.Models.ITraceRecord Deserialize(ref MessagePackReader reader, global::MessagePack.MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            if (reader.ReadArrayHeader() != 2)
            {
                throw new InvalidOperationException("Invalid Union data was detected. Type:global::Neo.Seattle.TraceDebug.Models.ITraceRecord");
            }

            options.Security.DepthStep(ref reader);
            var key = reader.ReadInt32();

            if (!this.keyToJumpMap.TryGetValue(key, out key))
            {
                key = -1;
            }

            global::Neo.Seattle.TraceDebug.Models.ITraceRecord result = null;
            switch (key)
            {
                case 0:
                    result = (global::Neo.Seattle.TraceDebug.Models.ITraceRecord)options.Resolver.GetFormatterWithVerify<global::Neo.Seattle.TraceDebug.Models.TracePoint>().Deserialize(ref reader, options);
                    break;
                case 1:
                    result = (global::Neo.Seattle.TraceDebug.Models.ITraceRecord)options.Resolver.GetFormatterWithVerify<global::Neo.Seattle.TraceDebug.Models.Notify>().Deserialize(ref reader, options);
                    break;
                case 2:
                    result = (global::Neo.Seattle.TraceDebug.Models.ITraceRecord)options.Resolver.GetFormatterWithVerify<global::Neo.Seattle.TraceDebug.Models.Log>().Deserialize(ref reader, options);
                    break;
                case 3:
                    result = (global::Neo.Seattle.TraceDebug.Models.ITraceRecord)options.Resolver.GetFormatterWithVerify<global::Neo.Seattle.TraceDebug.Models.Results>().Deserialize(ref reader, options);
                    break;
                case 4:
                    result = (global::Neo.Seattle.TraceDebug.Models.ITraceRecord)options.Resolver.GetFormatterWithVerify<global::Neo.Seattle.TraceDebug.Models.Fault>().Deserialize(ref reader, options);
                    break;
                default:
                    reader.Skip();
                    break;
            }

            reader.Depth--;
            return result;
        }
    }


}

#pragma warning restore 168
#pragma warning restore 414
#pragma warning restore 618
#pragma warning restore 612

#pragma warning restore SA1200 // Using directives should be placed correctly
#pragma warning restore SA1403 // File may only contain a single namespace
#pragma warning restore SA1649 // File name should match first type name
