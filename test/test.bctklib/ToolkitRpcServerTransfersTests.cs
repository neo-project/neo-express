// Copyright (C) 2015-2026 The Neo Project.
//
// ToolkitRpcServerTransfersTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit.Plugins;
using Neo.Cryptography.ECC;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using NeoArray = Neo.VM.Types.Array;

namespace test.bctklib
{
    public class ToolkitRpcServerTransfersTests : IDisposable
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

        public ToolkitRpcServerTransfersTests()
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

        // A contract-emitted Transfer event with fewer than from/to/amount items must not
        // abort getnep17transfers by throwing on the State[2] indexer.
        [Fact]
        public void GetNep17Transfers_skips_a_malformed_transfer_notification()
        {
            using var snapshot = new StoreCache(store.GetSnapshot());

            var malformed = new NotificationRecord(
                NativeContract.NEO.Hash, "Transfer", new NeoArray(), Neo.Network.P2P.Payloads.InventoryType.TX, UInt256.Zero);
            var provider = new StubNotificationsProvider(new NotificationInfo(0, 0, 0, malformed));

            var act = () => ToolkitRpcServer
                .GetNep17Transfers(snapshot, provider, UInt160.Zero, 0, ulong.MaxValue)
                .ToList();

            act.Should().NotThrow().Subject.Should().BeEmpty();
        }

        sealed class StubNotificationsProvider : INotificationsProvider
        {
            readonly NotificationInfo[] notifications;

            public StubNotificationsProvider(params NotificationInfo[] notifications) => this.notifications = notifications;

            public IEnumerable<NotificationInfo> GetNotifications(
                SeekDirection direction = SeekDirection.Forward,
                IReadOnlySet<UInt160>? contracts = null,
                IReadOnlySet<string>? eventNames = null) => notifications;
        }
    }
}
