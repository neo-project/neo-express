// Copyright (C) 2015-2024 The Neo Project.
//
// Instruction.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.IO;

namespace Neo.Collector.Models
{
    struct Instruction
    {
        public readonly OpCode OpCode;
        public readonly ArraySegment<byte> Operand;

        public int Size
        {
            get
            {
                int operandSize;
                switch (OpCode)
                {
                    case OpCode.PUSHDATA1:
                        operandSize = 1 + Operand.Count;
                        break;
                    case OpCode.PUSHDATA2:
                        operandSize = 2 + Operand.Count;
                        break;
                    case OpCode.PUSHDATA4:
                        operandSize = 4 + Operand.Count;
                        break;
                    default:
                        operandSize = GetOperandSize(OpCode);
                        break;
                }
                return 1 + operandSize;
            }
        }

        public Instruction(OpCode opCode)
            : this(opCode, Array.Empty<byte>())
        {
        }

        public Instruction(OpCode opCode, byte[] operand)
            : this(opCode, new ArraySegment<byte>(operand))
        {
        }

        public Instruction(OpCode opCode, ArraySegment<byte> operand)
        {
            OpCode = opCode;
            Operand = operand;
        }

        public static Instruction Parse(byte[] script, int address)
        {
            if (address >= script.Length)
                throw new ArgumentOutOfRangeException(nameof(address));

            var opCode = (OpCode)script[address];
            ArraySegment<byte> operand;
            switch (opCode)
            {
                case OpCode.PUSHDATA1:
                    {
                        int opSize = script[address + 1];
                        operand = new ArraySegment<byte>(script, address + 2, opSize);
                    }
                    break;
                case OpCode.PUSHDATA2:
                    {
                        int opSize = BitConverter.ToUInt16(script, address + 1);
                        operand = new ArraySegment<byte>(script, address + 3, opSize);
                    }
                    break;
                case OpCode.PUSHDATA4:
                    {
                        int opSize = BitConverter.ToInt32(script, address + 1);
                        operand = new ArraySegment<byte>(script, address + 1 + 4, opSize);
                    }
                    break;
                default:
                    {
                        var opSize = GetOperandSize(opCode);
                        operand = new ArraySegment<byte>(script, address + 1, opSize);
                    }
                    break;
            }
            return new Instruction(opCode, operand);
        }

        public static Instruction Parse(OpCode opCode, BinaryReader reader)
        {
            int opSize;
            switch (opCode)
            {
                case OpCode.PUSHDATA1:
                    opSize = reader.ReadByte();
                    break;
                case OpCode.PUSHDATA2:
                    opSize = reader.ReadUInt16();
                    break;
                case OpCode.PUSHDATA4:
                    opSize = reader.ReadInt32();
                    break;
                default:
                    opSize = GetOperandSize(opCode);
                    break;
            }

            var operand = opSize > 0
                ? reader.ReadBytes(opSize)
                : Array.Empty<byte>();
            return new Instruction(opCode, operand);
        }


        public static int GetOperandSize(OpCode opCode)
        {
            switch (opCode)
            {
                case OpCode.PUSHDATA1:
                case OpCode.PUSHDATA2:
                case OpCode.PUSHDATA4:
                    throw new ArgumentException(nameof(opCode));
                case OpCode.PUSHINT8:
                case OpCode.JMP:
                case OpCode.JMPEQ:
                case OpCode.JMPGE:
                case OpCode.JMPGT:
                case OpCode.JMPIF:
                case OpCode.JMPIFNOT:
                case OpCode.JMPLE:
                case OpCode.JMPLT:
                case OpCode.JMPNE:
                case OpCode.CALL:
                case OpCode.ENDTRY:
                case OpCode.INITSSLOT:
                case OpCode.LDSFLD:
                case OpCode.STSFLD:
                case OpCode.LDLOC:
                case OpCode.STLOC:
                case OpCode.LDARG:
                case OpCode.STARG:
                case OpCode.NEWARRAY_T:
                case OpCode.ISTYPE:
                case OpCode.CONVERT:
                    return 1;
                case OpCode.PUSHINT16:
                case OpCode.CALLT:
                case OpCode.TRY:
                case OpCode.INITSLOT:
                    return 2;
                case OpCode.PUSHINT32:
                case OpCode.PUSHA:
                case OpCode.JMP_L:
                case OpCode.JMPEQ_L:
                case OpCode.JMPGE_L:
                case OpCode.JMPGT_L:
                case OpCode.JMPIF_L:
                case OpCode.JMPIFNOT_L:
                case OpCode.JMPLE_L:
                case OpCode.JMPLT_L:
                case OpCode.JMPNE_L:
                case OpCode.CALL_L:
                case OpCode.ENDTRY_L:
                case OpCode.SYSCALL:
                    return 4;
                case OpCode.PUSHINT64:
                case OpCode.TRY_L:
                    return 8;
                case OpCode.PUSHINT128:
                    return 16;
                case OpCode.PUSHINT256:
                    return 32;
                default:
                    return 0;
            }
        }
    }
}
