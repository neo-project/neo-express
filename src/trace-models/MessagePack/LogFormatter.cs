#pragma warning disable 618
#pragma warning disable 612
#pragma warning disable 414
#pragma warning disable 168

#pragma warning disable SA1129 // Do not use default value type constructor
#pragma warning disable SA1200 // Using directives should be placed correctly
#pragma warning disable SA1309 // Field names should not begin with underscore
#pragma warning disable SA1312 // Variable names should begin with lower-case letter
#pragma warning disable SA1403 // File may only contain a single namespace
#pragma warning disable SA1649 // File name should match first type name

namespace Neo.Seattle.TraceDebug.Formatters
{
    using System;
    using System.Buffers;
    using MessagePack;
    using UInt160 = global::Neo.UInt160;

    public sealed class LogFormatter : global::MessagePack.Formatters.IMessagePackFormatter<global::Neo.Seattle.TraceDebug.Models.Log>
    {


        public void Serialize(ref MessagePackWriter writer, global::Neo.Seattle.TraceDebug.Models.Log value, global::MessagePack.MessagePackSerializerOptions options)
        {
            IFormatterResolver formatterResolver = options.Resolver;
            writer.WriteArrayHeader(2);
            formatterResolver.GetFormatterWithVerify<UInt160>().Serialize(ref writer, value.ScriptHash, options);
            formatterResolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Message, options);
        }

        public global::Neo.Seattle.TraceDebug.Models.Log Deserialize(ref MessagePackReader reader, global::MessagePack.MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                throw new InvalidOperationException("typecode is null, struct not supported");
            }

            options.Security.DepthStep(ref reader);
            IFormatterResolver formatterResolver = options.Resolver;
            var length = reader.ReadArrayHeader();
            var __ScriptHash__ = default(UInt160);
            var __Message__ = default(string);

            for (int i = 0; i < length; i++)
            {
                var key = i;

                switch (key)
                {
                    case 0:
                        __ScriptHash__ = formatterResolver.GetFormatterWithVerify<UInt160>().Deserialize(ref reader, options);
                        break;
                    case 1:
                        __Message__ = formatterResolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var ____result = new global::Neo.Seattle.TraceDebug.Models.Log(__ScriptHash__, __Message__);
            reader.Depth--;
            return ____result;
        }
    }
}

#pragma warning restore 168
#pragma warning restore 414
#pragma warning restore 618
#pragma warning restore 612

#pragma warning restore SA1129 // Do not use default value type constructor
#pragma warning restore SA1200 // Using directives should be placed correctly
#pragma warning restore SA1309 // Field names should not begin with underscore
#pragma warning restore SA1312 // Variable names should begin with lower-case letter
#pragma warning restore SA1403 // File may only contain a single namespace
#pragma warning restore SA1649 // File name should match first type name
