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
    using UInt160 = global::Neo.UInt160;
    using StorageItem = global::Neo.Ledger.StorageItem;

    public sealed class TracePointFormatter : global::MessagePack.Formatters.IMessagePackFormatter<global::Neo.Seattle.TraceDebug.Models.TracePoint>
    {
        public void Serialize(ref MessagePackWriter writer, global::Neo.Seattle.TraceDebug.Models.TracePoint value, global::MessagePack.MessagePackSerializerOptions options)
        {
            IFormatterResolver formatterResolver = options.Resolver;
            writer.WriteArrayHeader(3);
            formatterResolver.GetFormatterWithVerify<VMState>().Serialize(ref writer, value.State, options);
            formatterResolver.GetFormatterWithVerify<global::System.Collections.Generic.IReadOnlyList<global::Neo.Seattle.TraceDebug.Models.StackFrame>>().Serialize(ref writer, value.StackFrames, options);
            formatterResolver.GetFormatterWithVerify<global::System.Collections.Generic.IReadOnlyDictionary<UInt160, global::System.Collections.Generic.IReadOnlyDictionary<byte[], StorageItem>>>().Serialize(ref writer, value.Storages, options);
        }

        public global::Neo.Seattle.TraceDebug.Models.TracePoint Deserialize(ref MessagePackReader reader, global::MessagePack.MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                throw new InvalidOperationException("typecode is null, struct not supported");
            }

            options.Security.DepthStep(ref reader);
            IFormatterResolver formatterResolver = options.Resolver;
            var length = reader.ReadArrayHeader();
            var __State__ = default(VMState);
            var __StackFrames__ = default(global::System.Collections.Generic.IReadOnlyList<global::Neo.Seattle.TraceDebug.Models.StackFrame>);
            var __Storages__ = default(global::System.Collections.Generic.IReadOnlyDictionary<UInt160, global::System.Collections.Generic.IReadOnlyDictionary<byte[], StorageItem>>);

            for (int i = 0; i < length; i++)
            {
                var key = i;

                switch (key)
                {
                    case 0:
                        __State__ = formatterResolver.GetFormatterWithVerify<VMState>().Deserialize(ref reader, options);
                        break;
                    case 1:
                        __StackFrames__ = formatterResolver.GetFormatterWithVerify<global::System.Collections.Generic.IReadOnlyList<global::Neo.Seattle.TraceDebug.Models.StackFrame>>().Deserialize(ref reader, options);
                        break;
                    case 2:
                        __Storages__ = formatterResolver.GetFormatterWithVerify<global::System.Collections.Generic.IReadOnlyDictionary<UInt160, global::System.Collections.Generic.IReadOnlyDictionary<byte[], StorageItem>>>().Deserialize(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var ____result = new global::Neo.Seattle.TraceDebug.Models.TracePoint(__State__, __StackFrames__, __Storages__);
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
