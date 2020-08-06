using System;
using System.Buffers;
using System.Numerics;
using MessagePack;
using MessagePack.Formatters;

using StackItem = Neo.VM.Types.StackItem;
using StackItemType = Neo.VM.Types.StackItemType;
using PrimitiveType = Neo.VM.Types.PrimitiveType;

using NeoArray = Neo.VM.Types.Array;
using NeoBoolean = Neo.VM.Types.Boolean;
using NeoBuffer = Neo.VM.Types.Buffer;
using NeoByteString = Neo.VM.Types.ByteString;
using NeoInteger = Neo.VM.Types.Integer;
using NeoInteropInterface = Neo.VM.Types.InteropInterface;
using NeoMap = Neo.VM.Types.Map;
using NeoNull = Neo.VM.Types.Null;
using NeoPointer = Neo.VM.Types.Pointer;
using NeoStruct = Neo.VM.Types.Struct;
using Neo.Ledger;

namespace Neo.Seattle.TraceDebug.Formatters
{
    public class StorageItemFormatter : IMessagePackFormatter<StorageItem>
    {
        public readonly static StorageItemFormatter Instance = new StorageItemFormatter();

        public StorageItem Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null!;
            }

            options.Security.DepthStep(ref reader);

            var value = default(byte[]);
            var isConstant = default(bool);
            for (int key = 0; key < reader.ReadArrayHeader(); key++)
            {
                switch (key)
                {
                    case 0:
                        value= reader.ReadBytes()?.ToArray();
                        break;
                    case 1:
                        isConstant = reader.ReadBoolean();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var result = new StorageItem(value, isConstant);
            reader.Depth--;
            return result;
        }

        public void Serialize(ref MessagePackWriter writer, StorageItem value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(2);
            writer.Write(value.Value);
            writer.Write(value.IsConstant);
        }
    }
}
