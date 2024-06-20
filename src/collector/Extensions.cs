// Copyright (C) 2015-2024 The Neo Project.
//
// Extensions.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Collector.Models;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace Neo.Collector
{
    static class Extensions
    {
        public static bool TryParseHexString(this string @this, out byte[] buffer)
        {
            buffer = Array.Empty<byte>();
            if (@this.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                @this = @this.Substring(2);
            if (@this.Length % 2 != 0)
                return false;

            var length = @this.Length / 2;
            buffer = new byte[length];
            for (var i = 0; i < length; i++)
            {
                var str = @this.Substring(i * 2, 2);
                if (!byte.TryParse(str, NumberStyles.AllowHexSpecifier, null, out buffer[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public static ulong ReadVarInt(this BinaryReader @this, ulong max = ulong.MaxValue)
        {
            byte b = @this.ReadByte();
            ulong value;
            switch (b)
            {
                case 0xfd:
                    value = @this.ReadUInt16();
                    break;
                case 0xfe:
                    value = @this.ReadUInt32();
                    break;
                case 0xff:
                    value = @this.ReadUInt64();
                    break;
                default:
                    value = b;
                    break;
            }
            if (value > max)
                throw new FormatException();
            return value;
        }

        public static byte[] ReadVarMemory(this BinaryReader @this, int max = 0x1000000)
        {
            var length = (int)@this.ReadVarInt((ulong)max);
            return @this.ReadBytes(length);
        }

        public static string ReadVarString(this BinaryReader @this, int max = 0x1000000)
        {
            return Encoding.UTF8.GetString(@this.ReadVarMemory(max));
        }

        public static IEnumerable<(int address, Instruction instruction)> EnumerateInstructions(this byte[] script)
        {
            int address = 0;
            while (address < script.Length)
            {
                var instruction = Instruction.Parse(script, address);
                yield return (address, instruction);
                address += instruction.Size;
            }
        }

        public static IEnumerable<(int address, Instruction instruction)> EnumerateInstructions(this Stream stream)
        {
            int address = 0;
            var reader = new BinaryReader(stream);
            while (reader.TryReadOpCode(out var opCode))
            {
                var instruction = Instruction.Parse(opCode, reader);
                yield return (address, instruction);
                address += instruction.Size;
            }
        }

        static bool TryReadOpCode(this BinaryReader @this, out OpCode value)
        {
            try
            {
                value = (OpCode)@this.ReadByte();
                return true;
            }
            catch (EndOfStreamException)
            {
                value = default;
                return false;
            }
        }

        public static bool IsCallInstruction(this Instruction instruction)
            => instruction.OpCode == OpCode.CALL
                || instruction.OpCode == OpCode.CALL_L;

        public static bool IsBranchInstruction(this Instruction instruction)
            => IsBranchInstruction(instruction.OpCode);

        static bool IsBranchInstruction(OpCode opCode)
            => opCode >= OpCode.JMPIF && opCode <= OpCode.JMPLE_L;

        public static int GetCallOffset(this Instruction instruction)
        {
            switch (instruction.OpCode)
            {
                case OpCode.CALL_L:
                    return BinaryPrimitives.ReadInt32LittleEndian(instruction.Operand.AsSpan());
                case OpCode.CALL:
                    return (sbyte)instruction.Operand.AsSpan()[0];
                default:
                    return 0;
            }
        }

        public static int GetBranchOffset(this Instruction instruction)
        {
            switch (instruction.OpCode)
            {
                case OpCode.JMPIF_L:
                case OpCode.JMPIFNOT_L:
                case OpCode.JMPEQ_L:
                case OpCode.JMPNE_L:
                case OpCode.JMPGT_L:
                case OpCode.JMPGE_L:
                case OpCode.JMPLT_L:
                case OpCode.JMPLE_L:
                    return BinaryPrimitives.ReadInt32LittleEndian(instruction.Operand.AsSpan());
                case OpCode.JMPIF:
                case OpCode.JMPIFNOT:
                case OpCode.JMPEQ:
                case OpCode.JMPNE:
                case OpCode.JMPGT:
                case OpCode.JMPGE:
                case OpCode.JMPLT:
                case OpCode.JMPLE:
                    return (sbyte)instruction.Operand.AsSpan()[0];
                default:
                    return 0;
            }
        }

        public static decimal CalculateLineRate(this IEnumerable<NeoDebugInfo.SequencePoint> lines, Func<int, bool> hitFunc)
        {
            var (lineCount, hitCount) = GetLineRate(lines, hitFunc);
            return Utility.CalculateHitRate(lineCount, hitCount);
        }

        public static (uint lineCount, uint hitCount) GetLineRate(this IEnumerable<NeoDebugInfo.SequencePoint> lines, Func<int, bool> hitFunc)
        {
            uint lineCount = 0;
            uint hitCount = 0;
            foreach (var line in lines)
            {
                lineCount++;
                if (hitFunc(line.Address))
                { hitCount++; }
            }
            return (lineCount, hitCount);
        }


        public static decimal CalculateBranchRate(this IReadOnlyDictionary<int, Instruction> instructionMap, IEnumerable<NeoDebugInfo.Method> methods, Func<int, (uint, uint)> branchHitFunc)
        {
            var (branchCount, branchHit) = instructionMap.GetBranchRate(methods, branchHitFunc);
            return Utility.CalculateHitRate(branchCount, branchHit);
        }

        public static (uint branchCount, uint branchHit) GetBranchRate(this IReadOnlyDictionary<int, Instruction> instructionMap, IEnumerable<NeoDebugInfo.Method> methods, Func<int, (uint, uint)> branchHitFunc)
        {
            uint branchCount = 0u, branchHit = 0u;
            foreach (var method in methods)
            {
                var rate = instructionMap.GetBranchRate(method, branchHitFunc);
                branchCount += rate.branchCount;
                branchHit += rate.branchHit;
            }
            return (branchCount, branchHit);
        }

        public static decimal CalculateBranchRate(this IReadOnlyDictionary<int, Instruction> instructionMap, NeoDebugInfo.Method method, Func<int, (uint, uint)> branchHitFunc)
        {
            var (branchCount, branchHit) = instructionMap.GetBranchRate(method, branchHitFunc);
            return Utility.CalculateHitRate(branchCount, branchHit);
        }

        public static (uint branchCount, uint branchHit) GetBranchRate(this IReadOnlyDictionary<int, Instruction> instructionMap, NeoDebugInfo.Method method, Func<int, (uint, uint)> branchHitFunc)
        {
            uint branchCount = 0u, branchHit = 0u;
            for (int i = 0; i < method.SequencePoints.Count; i++)
            {
                var rate = instructionMap.GetBranchRate(method, i, branchHitFunc);
                branchCount += rate.branchCount;
                branchHit += rate.branchHit;
            }
            return (branchCount, branchHit);
        }

        public static decimal CalculateBranchRate(this IReadOnlyDictionary<int, Instruction> instructionMap, NeoDebugInfo.Method method, int index, Func<int, (uint, uint)> branchHitFunc)
        {
            var (branchCount, branchHit) = instructionMap.GetBranchRate(method, index, branchHitFunc);
            return Utility.CalculateHitRate(branchCount, branchHit);
        }

        public static (uint branchCount, uint branchHit) GetBranchRate(this IReadOnlyDictionary<int, Instruction> instructionMap, NeoDebugInfo.Method method, int index, Func<int, (uint, uint)> branchHitFunc)
        {
            return instructionMap
                .GetBranchInstructions(method, index)
                .GetBranchRate(branchHitFunc);
        }

        public static (uint branchCount, uint branchHit) GetBranchRate(this IEnumerable<(int address, OpCode opCode)> branchLines, Func<int, (uint, uint)> hitFunc)
        {
            var branchCount = 0u;
            var branchHit = 0u;
            foreach (var (address, opCode) in branchLines)
            {
                Debug.Assert(IsBranchInstruction(opCode));
                var (branchHitCount, continueHitCount) = hitFunc(address);
                branchCount += 2;
                branchHit += branchHitCount == 0 ? 0u : 1u;
                branchHit += continueHitCount == 0 ? 0u : 1u;
            }
            return (branchCount, branchHit);
        }

        public static IEnumerable<(int address, OpCode opCode)> GetBranchInstructions(this IReadOnlyDictionary<int, Instruction> instructionMap, NeoDebugInfo.Method method, int index)
        {
            var address = method.SequencePoints[index].Address;
            var last = instructionMap.GetLineLastAddress(method, index);

            while (address <= last)
            {
                if (instructionMap.TryGetValue(address, out var ins))
                {
                    if (ins.IsBranchInstruction())
                    {
                        yield return (address, ins.OpCode);
                    }
                    address += ins.Size;
                }
                else
                {
                    break;
                }
            }
        }

        public static int GetLineLastAddress(this IReadOnlyDictionary<int, Instruction> instructionMap, NeoDebugInfo.Method method, int index)
        {
            var point = method.SequencePoints[index];
            var nextIndex = index + 1;
            if (nextIndex >= method.SequencePoints.Count)
            {
                // if we're on the last SP of the method, return the method end address
                return method.Range.End;
            }
            else
            {
                var nextSPAddress = method.SequencePoints[index + 1].Address;
                var address = point.Address;
                while (true)
                {
                    if (instructionMap.TryGetValue(address, out var ins))
                    {
                        var newAddress = address + ins.Size;
                        if (newAddress >= nextSPAddress)
                        {
                            return address;
                        }
                        else
                        {
                            address = newAddress;
                        }
                    }
                    else
                    {
                        return address;
                    }
                }
            }
        }
    }
}
