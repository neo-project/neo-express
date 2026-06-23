// Copyright (C) 2015-2026 The Neo Project.
//
// ApplicationEngineAssertionsTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.Assertions;
using Neo.Cryptography.ECC;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.SmartContract;
using Neo.VM;
using System;
using Xunit;

namespace test.assertions;

public class ApplicationEngineAssertionsTests : IDisposable
{
    static readonly ProtocolSettings Settings = new()
    {
        Network = 0x334F454Eu,
        AddressVersion = ProtocolSettings.Default.AddressVersion,
        StandbyCommittee =
        [
            ECPoint.Parse("03b209fd4f53a7170ea4444e0cb0a6bb6a53c2bd016926989cf85f9b0fba17a70c", ECCurve.Secp256r1),
        ],
        ValidatorsCount = 1,
        SeedList = [],
        MillisecondsPerBlock = ProtocolSettings.Default.MillisecondsPerBlock,
        MaxTransactionsPerBlock = ProtocolSettings.Default.MaxTransactionsPerBlock,
        MemoryPoolMaxTransactions = ProtocolSettings.Default.MemoryPoolMaxTransactions,
        MaxTraceableBlocks = ProtocolSettings.Default.MaxTraceableBlocks,
        InitialGasDistribution = ProtocolSettings.Default.InitialGasDistribution,
        Hardforks = ProtocolSettings.Default.Hardforks,
    };

    readonly MemoryStore store = new();

    public ApplicationEngineAssertionsTests()
    {
        var block = NeoSystem.CreateGenesisBlock(Settings);
        using var snapshot = new StoreCache(store.GetSnapshot());
        Persist(snapshot, block, TriggerType.OnPersist, ApplicationEngine.System_Contract_NativeOnPersist);
        Persist(snapshot, block, TriggerType.PostPersist, ApplicationEngine.System_Contract_NativePostPersist);
        snapshot.Commit();

        static void Persist(StoreCache snapshot, Neo.Network.P2P.Payloads.Block block, TriggerType trigger, uint syscall)
        {
            using var engine = ApplicationEngine.Create(trigger, null, snapshot, block, Settings, 0L);
            using var sb = new ScriptBuilder();
            sb.EmitSysCall(syscall);
            engine.LoadScript(sb.ToArray());
            if (engine.Execute() != VMState.HALT)
                throw new InvalidOperationException("genesis persist failed", engine.FaultException);
        }
    }

    public void Dispose() => store.Dispose();

    ApplicationEngine Run(OpCode opcode)
    {
        var snapshot = new StoreCache(store.GetSnapshot());
        var engine = ApplicationEngine.Create(
            TriggerType.Application, null, snapshot, null, Settings, ApplicationEngine.TestModeGas);
        engine.LoadScript(new[] { (byte)opcode });
        engine.Execute();
        return engine;
    }

    [Fact]
    public void Halt_passes_and_Fault_fails_for_a_halted_engine()
    {
        using var engine = Run(OpCode.RET);

        engine.State.Should().Be(VMState.HALT);
        ((Action)(() => engine.Should().Halt())).Should().NotThrow();
        ((Action)(() => engine.Should().Fault())).Should().Throw<Xunit.Sdk.XunitException>();
    }

    [Fact]
    public void Fault_passes_and_Halt_fails_for_a_faulted_engine()
    {
        using var engine = Run(OpCode.ABORT);

        engine.State.Should().Be(VMState.FAULT);
        ((Action)(() => engine.Should().Fault())).Should().NotThrow();
        ((Action)(() => engine.Should().Halt())).Should().Throw<Xunit.Sdk.XunitException>();
    }

    [Fact]
    public void Halt_failure_message_surfaces_the_fault_exception()
    {
        using var engine = Run(OpCode.ABORT);

        var act = () => engine.Should().Halt();

        act.Should().Throw<Xunit.Sdk.XunitException>()
            .WithMessage("*FAULT*")
            .WithMessage("*exception*");
    }

    [Fact]
    public void Fault_failure_message_reports_the_result_stack_and_gas_for_a_halted_engine()
    {
        using var engine = Run(OpCode.RET);

        var act = () => engine.Should().Fault();

        // A HALT engine carries a result stack and a fee figure; surfacing them in the
        // failure message points at why execution succeeded instead of faulting.
        act.Should().Throw<Xunit.Sdk.XunitException>()
            .WithMessage("*HALT*")
            .WithMessage("*result stack count*")
            .WithMessage("*fee consumed*");
    }
}
