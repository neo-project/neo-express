// <auto-generated>
// THIS (.cs) FILE IS GENERATED BY MPC(MessagePack-CSharp). DO NOT CHANGE IT.
// </auto-generated>

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
    using VMState = global::Neo.VM.VMState;

    public sealed class ResultsFormatter : global::MessagePack.Formatters.IMessagePackFormatter<global::Neo.Seattle.TraceDebug.Models.Results>
    {


        public void Serialize(ref MessagePackWriter writer, global::Neo.Seattle.TraceDebug.Models.Results value, global::MessagePack.MessagePackSerializerOptions options)
        {
            IFormatterResolver formatterResolver = options.Resolver;
            writer.WriteArrayHeader(3);
            formatterResolver.GetFormatterWithVerify<VMState>().Serialize(ref writer, value.State, options);
            writer.Write(value.GasConsumed);
            formatterResolver.GetFormatterWithVerify<global::System.Collections.Generic.IReadOnlyList<global::Neo.VM.Types.StackItem>>().Serialize(ref writer, value.ResultStack, options);
        }

        public global::Neo.Seattle.TraceDebug.Models.Results Deserialize(ref MessagePackReader reader, global::MessagePack.MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                throw new InvalidOperationException("typecode is null, struct not supported");
            }

            options.Security.DepthStep(ref reader);
            IFormatterResolver formatterResolver = options.Resolver;
            var length = reader.ReadArrayHeader();
            var __State__ = default(VMState);
            var __GasConsumed__ = default(long);
            var __ResultStack__ = default(global::System.Collections.Generic.IReadOnlyList<global::Neo.VM.Types.StackItem>);

            for (int i = 0; i < length; i++)
            {
                var key = i;

                switch (key)
                {
                    case 0:
                        __State__ = formatterResolver.GetFormatterWithVerify<VMState>().Deserialize(ref reader, options);
                        break;
                    case 1:
                        __GasConsumed__ = reader.ReadInt64();
                        break;
                    case 2:
                        __ResultStack__ = formatterResolver.GetFormatterWithVerify<global::System.Collections.Generic.IReadOnlyList<global::Neo.VM.Types.StackItem>>().Deserialize(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var ____result = new global::Neo.Seattle.TraceDebug.Models.Results(__State__, __GasConsumed__, __ResultStack__);
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
