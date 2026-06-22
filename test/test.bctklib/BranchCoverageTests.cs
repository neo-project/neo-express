// Copyright (C) 2015-2026 The Neo Project.
//
// BranchCoverageTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.BlockchainToolkit.SmartContract;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace test.bctklib
{
    public class BranchCoverageTests : IClassFixture<DeployedContractFixture>
    {
        readonly DeployedContractFixture deployedContractFixture;

        public BranchCoverageTests(DeployedContractFixture deployedContractFixture)
        {
            this.deployedContractFixture = deployedContractFixture;
        }

        // Builds a small loop whose single conditional branch is taken more than once:
        //
        //         push 3
        //   loop: dec                 ; counter = counter - 1
        //         dup
        //         push 0
        //         jmpgt loop          ; if counter > 0 goto loop (taken while counter > 0)
        //         drop
        //         ret
        //
        // Starting from 3 the branch instruction is evaluated three times and taken
        // twice (counter 2 and 1) before falling through at counter 0. The recorded
        // branch-taken count must therefore accumulate to 2.
        [Fact]
        public void Branch_taken_count_accumulates_across_iterations()
        {
            using var sb = new ScriptBuilder();
            sb.EmitPush(3);
            var loop = sb.Length;
            sb.Emit(OpCode.DEC);
            sb.Emit(OpCode.DUP);
            sb.EmitPush(0);
            var branchIP = sb.Length;
            sb.EmitJump(OpCode.JMPGT_L, loop - branchIP);
            sb.Emit(OpCode.DROP);
            sb.Emit(OpCode.RET);
            var script = sb.ToArray();

            // Pass a mock file system so the test is unaffected by the process-global
            // coverage path environment variable that other engine tests may set.
            using var snapshot = new StoreCache(deployedContractFixture.Store.GetSnapshot());
            using var engine = new TestApplicationEngine(snapshot, fileSystem: new MockFileSystem());

            engine.LoadScript(script);
            engine.Execute().Should().Be(VMState.HALT);

            var scriptHash = Neo.SmartContract.Helper.ToScriptHash(script);
            var branchMap = engine.GetBranchMap(scriptHash);

            branchMap.Should().ContainKey(branchIP);
            branchMap[branchIP].branchCount.Should().Be(2);
        }
    }
}
