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

    public sealed class StackFrameFormatter : global::MessagePack.Formatters.IMessagePackFormatter<global::Neo.Seattle.TraceDebug.Models.StackFrame>
    {


        public void Serialize(ref MessagePackWriter writer, global::Neo.Seattle.TraceDebug.Models.StackFrame value, global::MessagePack.MessagePackSerializerOptions options)
        {
            IFormatterResolver formatterResolver = options.Resolver;
            writer.WriteArrayHeader(6);
            formatterResolver.GetFormatterWithVerify<UInt160>().Serialize(ref writer, value.ScriptHash, options);
            writer.Write(value.InstructionPointer);
            formatterResolver.GetFormatterWithVerify<global::System.Collections.Generic.IReadOnlyList<global::Neo.VM.Types.StackItem>>().Serialize(ref writer, value.EvaluationStack, options);
            formatterResolver.GetFormatterWithVerify<global::System.Collections.Generic.IReadOnlyList<global::Neo.VM.Types.StackItem>>().Serialize(ref writer, value.LocalVariables, options);
            formatterResolver.GetFormatterWithVerify<global::System.Collections.Generic.IReadOnlyList<global::Neo.VM.Types.StackItem>>().Serialize(ref writer, value.StaticFields, options);
            formatterResolver.GetFormatterWithVerify<global::System.Collections.Generic.IReadOnlyList<global::Neo.VM.Types.StackItem>>().Serialize(ref writer, value.Arguments, options);
        }

        public global::Neo.Seattle.TraceDebug.Models.StackFrame Deserialize(ref MessagePackReader reader, global::MessagePack.MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                throw new InvalidOperationException("typecode is null, struct not supported");
            }

            options.Security.DepthStep(ref reader);
            IFormatterResolver formatterResolver = options.Resolver;
            var length = reader.ReadArrayHeader();
            var __ScriptHash__ = default(UInt160);
            var __InstructionPointer__ = default(int);
            var __EvaluationStack__ = default(global::System.Collections.Generic.IReadOnlyList<global::Neo.VM.Types.StackItem>);
            var __LocalVariables__ = default(global::System.Collections.Generic.IReadOnlyList<global::Neo.VM.Types.StackItem>);
            var __StaticFields__ = default(global::System.Collections.Generic.IReadOnlyList<global::Neo.VM.Types.StackItem>);
            var __Arguments__ = default(global::System.Collections.Generic.IReadOnlyList<global::Neo.VM.Types.StackItem>);

            for (int i = 0; i < length; i++)
            {
                var key = i;

                switch (key)
                {
                    case 0:
                        __ScriptHash__ = formatterResolver.GetFormatterWithVerify<UInt160>().Deserialize(ref reader, options);
                        break;
                    case 1:
                        __InstructionPointer__ = reader.ReadInt32();
                        break;
                    case 2:
                        __EvaluationStack__ = formatterResolver.GetFormatterWithVerify<global::System.Collections.Generic.IReadOnlyList<global::Neo.VM.Types.StackItem>>().Deserialize(ref reader, options);
                        break;
                    case 3:
                        __LocalVariables__ = formatterResolver.GetFormatterWithVerify<global::System.Collections.Generic.IReadOnlyList<global::Neo.VM.Types.StackItem>>().Deserialize(ref reader, options);
                        break;
                    case 4:
                        __StaticFields__ = formatterResolver.GetFormatterWithVerify<global::System.Collections.Generic.IReadOnlyList<global::Neo.VM.Types.StackItem>>().Deserialize(ref reader, options);
                        break;
                    case 5:
                        __Arguments__ = formatterResolver.GetFormatterWithVerify<global::System.Collections.Generic.IReadOnlyList<global::Neo.VM.Types.StackItem>>().Deserialize(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var ____result = new global::Neo.Seattle.TraceDebug.Models.StackFrame(__ScriptHash__, __InstructionPointer__, __EvaluationStack__, __LocalVariables__, __StaticFields__, __Arguments__);
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
