// Copyright (C) 2015-2026 The Neo Project.
//
// GetOperandStringTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.BlockchainToolkit;
using Neo.VM;
using System.Linq;
using Xunit;

namespace test.bctklib
{
    public class GetOperandStringTests
    {
        [Fact]
        public void multi_byte_operand_is_dash_separated_uppercase_hex()
        {
            using var sb = new ScriptBuilder();
            sb.Emit(OpCode.SYSCALL, new byte[] { 0xAA, 0xBB, 0xCC, 0xDD });
            var instruction = new Script(sb.ToArray()).EnumerateInstructions().First().instruction;

            instruction.OpCode.Should().Be(OpCode.SYSCALL);
            instruction.GetOperandString().Should().Be("AA-BB-CC-DD");
        }

        [Fact]
        public void single_byte_operand_has_no_separator()
        {
            using var sb = new ScriptBuilder();
            sb.Emit(OpCode.JMP, new byte[] { 0x0F });
            var instruction = new Script(sb.ToArray()).EnumerateInstructions().First().instruction;

            instruction.OpCode.Should().Be(OpCode.JMP);
            instruction.GetOperandString().Should().Be("0F");
        }
    }
}
