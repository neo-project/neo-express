// Copyright (C) 2015-2026 The Neo Project.
//
// DebugApplicationEngineTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.Cryptography.ECC;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using NeoDebug.Neo3;
using System;
using System.Linq;
using Xunit;

namespace test.neodebug
{
    // Exercises the live (in-process) engine over a raw VM script: stepping advances the instruction
    // pointer, backward stepping is unsupported, and the result/state surface through IApplicationEngine.
    public class DebugApplicationEngineTests
    {
        // ProtocolSettings.Default has no committee, so genesis can't be created; use a single-member committee.
        private static readonly ProtocolSettings Settings = ProtocolSettings.Default with
        {
            StandbyCommittee = new[] { ECPoint.Parse("03b209fd4f53a7170ea4444e0cb0a6bb6a53c2bd016926989cf85f9b0fba17a70c", ECCurve.Secp256r1) },
            ValidatorsCount = 1,
        };

        private static DebugApplicationEngine NewEngine()
        {
            var store = new MemoryStore();
            InitializeLedger(store);
            var snapshot = new StoreCache(store.GetSnapshot());
            return new DebugApplicationEngine(null, snapshot, Settings, null, null);
        }

        // Persists the genesis block so the ApplicationEngine ctor can read the ledger's current index.
        // (bctklib's EnsureLedgerInitialized is a stub that does not actually persist anything.)
        private static void InitializeLedger(IStore store)
        {
            using var snapshot = new StoreCache(store.GetSnapshot());
            var block = NeoSystem.CreateGenesisBlock(Settings);

            using (var engine = ApplicationEngine.Create(TriggerType.OnPersist, null, snapshot, block, Settings, 0L))
            {
                using var sb = new ScriptBuilder();
                sb.EmitSysCall(ApplicationEngine.System_Contract_NativeOnPersist);
                engine.LoadScript(sb.ToArray());
                if (engine.Execute() != VMState.HALT)
                    throw new InvalidOperationException("NativeOnPersist failed", engine.FaultException);
            }

            using (var engine = ApplicationEngine.Create(TriggerType.PostPersist, null, snapshot, block, Settings, 0L))
            {
                using var sb = new ScriptBuilder();
                sb.EmitSysCall(ApplicationEngine.System_Contract_NativePostPersist);
                engine.LoadScript(sb.ToArray());
                if (engine.Execute() != VMState.HALT)
                    throw new InvalidOperationException("NativePostPersist failed", engine.FaultException);
            }

            snapshot.Commit();
        }

        private static Script AddScript()
        {
            using var builder = new ScriptBuilder();
            builder.Emit(OpCode.PUSH1);
            builder.Emit(OpCode.PUSH2);
            builder.Emit(OpCode.ADD);
            builder.Emit(OpCode.RET);
            return new Script(builder.ToArray());
        }

        [Fact]
        public void does_not_support_stepping_backward()
        {
            using var engine = NewEngine();
            IApplicationEngine iface = engine;

            Assert.False(iface.SupportsStepBack);
            Assert.True(iface.AtStart);
            Assert.Throws<NotSupportedException>(() => iface.ExecutePrevInstruction());
        }

        [Fact]
        public void steps_forward_through_a_live_script_to_halt()
        {
            using var engine = NewEngine();
            engine.LoadScript(AddScript());
            IApplicationEngine iface = engine;

            iface.ExecuteNextInstruction(); // execute PUSH1
            Assert.False(iface.AtStart);
            Assert.Equal(1, iface.CurrentContext!.InstructionPointer);

            for (int i = 0; i < 8 && iface.State != VMState.HALT && iface.State != VMState.FAULT && iface.CurrentContext is not null; i++)
            {
                iface.ExecuteNextInstruction();
            }

            Assert.Equal(VMState.HALT, iface.State);
            Assert.Single(iface.ResultStack);
            Assert.Equal(3, (int)iface.ResultStack[0].GetInteger());
        }

        [Fact]
        public void reports_no_contract_or_storage_for_an_unknown_hash()
        {
            using var engine = NewEngine();
            IApplicationEngine iface = engine;

            Assert.False(iface.TryGetContract(UInt160.Zero, out _));
            Assert.Empty(iface.GetStorages(UInt160.Zero));
            Assert.Equal(Settings.AddressVersion, iface.AddressVersion);
        }
    }
}
