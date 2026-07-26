// Copyright (C) 2015-2026 The Neo Project.
//
// Extensions.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Cryptography.ECC;
using Neo.Extensions;
using Neo.SmartContract;
using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;
using ByteString = Neo.VM.Types.ByteString;
using StackItem = Neo.VM.Types.StackItem;
using StackItemType = Neo.VM.Types.StackItemType;

namespace NeoDebug.Neo3
{
    internal static class Extensions
    {
        public static bool StartsWith<T>(this ReadOnlyMemory<T> @this, ReadOnlySpan<T> value)
            where T : IEquatable<T>
        {
            return @this.Length >= value.Length
                && @this[..value.Length].Span.SequenceEqual(value);
        }

        public static bool TryGetMethod(this DebugInfo? debugInfo, int instructionPointer, [MaybeNullWhen(false)] out DebugInfo.Method method)
        {
            if (debugInfo is not null)
            {
                foreach (var m in debugInfo.Methods)
                {
                    if (m.Range.Start <= instructionPointer && instructionPointer <= m.Range.End)
                    {
                        method = m;
                        return true;
                    }
                }
            }
            method = default;
            return false;
        }

        public static bool TryGetDocumentPath(this DebugInfo.SequencePoint @this, DebugInfo? debugInfo, out string path)
        {
            if (debugInfo is not null && @this.Document < debugInfo.Documents.Count)
            {
                path = debugInfo.Documents[@this.Document];
                return true;
            }

            path = "";
            return false;
        }

        public static bool PathEquals(this DebugInfo.SequencePoint @this, DebugInfo? debugInfo, string path)
        {
            return @this.TryGetDocumentPath(debugInfo, out var documentPath)
                && string.Equals(path, documentPath, StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryGetCurrentSequencePoint(this DebugInfo.Method? method, int instructionPointer, out DebugInfo.SequencePoint sequencePoint)
        {
            if (method.HasValue)
            {
                var sequencePoints = method.Value.SequencePoints;
                if (sequencePoints.Count > 0)
                {
                    for (int i = sequencePoints.Count - 1; i >= 0; i--)
                    {
                        if (instructionPointer >= sequencePoints[i].Address)
                        {
                            sequencePoint = sequencePoints[i];
                            return true;
                        }
                    }

                    sequencePoint = sequencePoints[0];
                    return true;
                }
            }
            sequencePoint = default;
            return false;
        }

        public static bool TryFind<T>(this IEnumerable<T> @this, Predicate<T> predicate, [MaybeNullWhen(false)] out T value)
        {
            foreach (var v in @this)
            {
                if (predicate(v))
                {
                    value = v;
                    return true;
                }
            }
            value = default;
            return false;
        }

        public static string ToMapKeyString(this Neo.VM.Types.PrimitiveType item)
        {
            return item switch
            {
                Neo.VM.Types.Boolean @bool => @bool.GetBoolean().ToString(),
                Neo.VM.Types.ByteString byteString => byteString.GetSpan().ToHexString(),
                Neo.VM.Types.Integer @int => @int.GetInteger().ToString(),
                _ => item.GetSpan().ToHexString(),
            };
        }

        public static JToken ToJson(this StackItem item)
        {
            return item switch
            {
                Neo.VM.Types.Boolean _ => item.GetBoolean(),
                Neo.VM.Types.Buffer buffer => buffer.GetSpan().ToHexString(),
                Neo.VM.Types.ByteString byteString => byteString.GetSpan().ToHexString(),
                Neo.VM.Types.Integer @int => @int.GetInteger().ToString(),
                Neo.VM.Types.Map map => MapToJson(map),
                Neo.VM.Types.Null _ => new JValue((object?)null),
                Neo.VM.Types.Array array => new JArray(array.Select(i => i.ToJson())),
                _ => throw new NotSupportedException(),
            };

            static JObject MapToJson(Neo.VM.Types.Map map)
            {
                var json = new JObject();
                foreach (var (key, value) in map)
                {
                    json.Add(PrimitiveTypeToString(key), value.ToJson());
                }
                return json;
            }

            static string PrimitiveTypeToString(Neo.VM.Types.PrimitiveType item)
            {
                try
                {
                    return item.GetString() ?? throw new Exception();
                }
                catch
                {
                    return Convert.ToHexString(item.GetSpan());
                }
            }
        }

        public static Variable ToVariable(this StackItem item, IVariableManager manager, string name, ContractParameterType parameterType)
        {
            try
            {
                Variable? variable = parameterType switch
                {
                    ContractParameterType.Boolean => NewVariable(item.GetBoolean()),
                    ContractParameterType.ByteArray => ConvertByteArray(),
                    ContractParameterType.Hash160 => NewVariable(new UInt160(item.GetSpan())),
                    ContractParameterType.Hash256 => NewVariable(new UInt256(item.GetSpan())),
                    ContractParameterType.Integer => NewVariable(item.GetInteger()),
                    ContractParameterType.PublicKey => NewVariable(ECPoint.DecodePoint(item.GetSpan(), ECCurve.Secp256r1)),
                    ContractParameterType.Signature => ConvertByteArray(),
                    ContractParameterType.String => NewVariable(item.GetString()),
                    _ => null,
                };

                if (variable != null)
                    return variable;
            }
            catch { }

            return item.ToVariable(manager, name);

            Variable? NewVariable(object? obj) => obj == null ? null : new Variable { Name = name, Value = obj.ToString(), Type = parameterType.ToString() };

            Variable? ConvertByteArray()
            {
                if (item.IsNull)
                    return new Variable { Name = name, Value = "<null>", Type = parameterType.ToString() };
                if (item is Neo.VM.Types.Buffer buffer)
                    return ByteArrayContainer.Create(manager, buffer, name);
                if (item is ByteString byteString)
                    return ByteArrayContainer.Create(manager, byteString, name);
                if (item is Neo.VM.Types.PrimitiveType)
                {
                    byteString = (ByteString)item.ConvertTo(StackItemType.ByteString);
                    return ByteArrayContainer.Create(manager, (ReadOnlyMemory<byte>)byteString, name);
                }
                return null;
            }
        }

        public static Variable ToVariable(this StackItem item, IVariableManager manager, string name)
        {
            return item switch
            {
                Neo.VM.Types.Array array => NeoArrayContainer.Create(manager, array, name),
                Neo.VM.Types.Boolean _ => new Variable { Name = name, Value = $"{item.GetBoolean()}", Type = "Boolean" },
                Neo.VM.Types.Buffer buffer => ByteArrayContainer.Create(manager, buffer, name),
                Neo.VM.Types.ByteString byteString => ByteArrayContainer.Create(manager, byteString, name),
                Neo.VM.Types.Integer @int => new Variable { Name = name, Value = $"{@int.GetInteger()}", Type = "Integer" },
                Neo.VM.Types.InteropInterface _ => new Variable { Name = name, Value = "InteropInterface" },
                Neo.VM.Types.Map map => NeoMapContainer.Create(manager, map, name),
                Neo.VM.Types.Null _ => new Variable { Name = name, Value = "<null>", Type = "Null" },
                Neo.VM.Types.Pointer _ => new Variable { Name = name, Value = "Pointer" },
                _ => throw new NotSupportedException(),
            };
        }

        public static BigDecimal AsBigDecimal(this long value, byte? decimals = null)
        {
            decimals ??= Neo.SmartContract.Native.NativeContract.GAS.Decimals;
            return new BigDecimal((System.Numerics.BigInteger)value, decimals.Value);
        }
    }
}
