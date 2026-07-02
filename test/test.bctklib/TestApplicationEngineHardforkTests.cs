// Copyright (C) 2015-2026 The Neo Project.
//
// TestApplicationEngineHardforkTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.SmartContract;
using Neo.BlockchainToolkit.TraceDebug;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.SmartContract;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO.Abstractions.TestingHelpers;
using Xunit;
using ExecutionContext = Neo.VM.ExecutionContext;

namespace test.bctklib;

public class TestApplicationEngineHardforkTests
{
    [Fact]
    public void Test_engine_uses_the_hardfork_jump_table_for_the_persisting_block()
    {
        var script = CreateGorgonSensitiveScript();
        var settings = DeployedContractFixture.Default with
        {
            Hardforks = DeployedContractFixture.Default.Hardforks
                .SetItem(Hardfork.HF_Echidna, 10)
                .SetItem(Hardfork.HF_Gorgon, 20)
        };

        using var preGorgon = Execute(script, settings, 15);
        preGorgon.State.Should().Be(VMState.HALT);

        using var gorgon = Execute(script, settings, 20);
        gorgon.State.Should().Be(VMState.FAULT);
        gorgon.FaultException.Should().BeOfType<InvalidCastException>();
    }

    [Fact]
    public void Trace_engine_uses_the_hardfork_jump_table_for_the_persisting_block()
    {
        var script = CreateGorgonSensitiveScript();
        var settings = DeployedContractFixture.Default with
        {
            Hardforks = DeployedContractFixture.Default.Hardforks
                .SetItem(Hardfork.HF_Echidna, 10)
                .SetItem(Hardfork.HF_Gorgon, 20)
        };

        using var preGorgon = ExecuteTrace(script, settings, 15);
        preGorgon.State.Should().Be(VMState.HALT);

        using var gorgon = ExecuteTrace(script, settings, 20);
        gorgon.State.Should().Be(VMState.FAULT);
        gorgon.FaultException.Should().BeOfType<InvalidCastException>();
    }

    static byte[] CreateGorgonSensitiveScript()
    {
        using var sb = new ScriptBuilder();
        sb.Emit(OpCode.NEWARRAY0);
        sb.EmitPush(0);
        sb.Emit(OpCode.SHL);
        return sb.ToArray();
    }

    static TestApplicationEngine Execute(byte[] script, ProtocolSettings settings, uint blockIndex)
    {
        using var store = new MemoryStore();
        store.EnsureLedgerInitialized(settings);
        using var snapshot = new StoreCache(store.GetSnapshot());
        var block = CreateBlock(blockIndex);
        var engine = new TestApplicationEngine(snapshot, persistingBlock: block, settings: settings, fileSystem: new MockFileSystem());
        engine.LoadScript(script);
        engine.Execute();
        return engine;
    }

    static TraceApplicationEngine ExecuteTrace(byte[] script, ProtocolSettings settings, uint blockIndex)
    {
        using var store = new MemoryStore();
        store.EnsureLedgerInitialized(settings);
        using var snapshot = new StoreCache(store.GetSnapshot());
        var block = CreateBlock(blockIndex);
        var engine = new TraceApplicationEngine(
            new NullTraceDebugSink(),
            TriggerType.Application,
            TestApplicationEngine.CreateTestTransaction(),
            snapshot,
            block,
            settings,
            ApplicationEngine.TestModeGas);
        engine.LoadScript(script);
        engine.Execute();
        return engine;
    }

    static Block CreateBlock(uint index) => new()
    {
        Header = new Header
        {
            PrevHash = UInt256.Zero,
            MerkleRoot = UInt256.Zero,
            Index = index,
            NextConsensus = UInt160.Zero,
            Witness = Witness.Empty
        },
        Transactions = []
    };

    sealed class NullTraceDebugSink : ITraceDebugSink
    {
        public void Dispose()
        {
        }

        public void Fault(Exception exception)
        {
        }

        public void Log(LogEventArgs args, string scriptName)
        {
        }

        public void Notify(NotifyEventArgs args, string scriptName)
        {
        }

        public void ProtocolSettings(uint network, byte addressVersion)
        {
        }

        public void Results(VMState vmState, long gasConsumed, IReadOnlyCollection<Neo.VM.Types.StackItem> results)
        {
        }

        public void Script(Script script)
        {
        }

        public void Storages(UInt160 scriptHash, IEnumerable<(StorageKey key, StorageItem item)> storages)
        {
        }

        public void Trace(VMState vmState, long gasConsumed, IReadOnlyCollection<ExecutionContext> executionContexts)
        {
        }
    }
}
