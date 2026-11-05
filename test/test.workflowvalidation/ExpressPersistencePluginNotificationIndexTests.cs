// Copyright (C) 2015-2026 The Neo Project.
//
// ExpressPersistencePluginNotificationIndexTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.Cryptography;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.SmartContract;
using Neo.VM;
using NeoExpress.Node;
using Newtonsoft.Json;
using System.IO;
using System.Reflection;
using Xunit;

namespace test.workflowvalidation;

[CollectionDefinition("ExpressPersistencePluginNotificationIndex", DisableParallelization = true)]
public sealed class ExpressPersistencePluginNotificationIndexCollection { }

// StageApplicationLogs persists notifications under the key
// (blockIndex, txIndex, j) where txIndex is the transaction's position within
// block.Transactions. ExpressRpcServerPlugin.GetTransfers returns that txIndex
// verbatim as ["transfernotifyindex"] alongside ["blockindex"] and ["txhash"],
// so RPC consumers correlate it against the block's transaction list. Faulted
// transactions are persisted in block.Transactions and therefore occupy
// positions: the txIndex counter must count FAULT executions too, even though
// their notifications are not stored (gettransfers must not surface transfers
// from failed transactions).
//
// The plugin's OnCommitting/OnCommitted handlers and its store fields are
// private, so these tests reflection-drive the real staging pipeline with
// fabricated-but-real ApplicationExecuted values: each is built through the
// internal ApplicationExecuted(ApplicationEngine) constructor over a genuine
// ApplicationEngine (HALT/FAULT from a real RET/THROW execution), with
// notifications installed as real NotifyEventArgs instances in the engine's
// private notifications list.
[Collection("ExpressPersistencePluginNotificationIndex")]
public class ExpressPersistencePluginNotificationIndexTests
{
    const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    static readonly FieldInfo AppLogsStoreField =
        typeof(ExpressPersistencePlugin).GetField("appLogsStore", AnyInstance)
        ?? throw new InvalidOperationException("appLogsStore field not found");
    static readonly FieldInfo NotificationsStoreField =
        typeof(ExpressPersistencePlugin).GetField("notificationsStore", AnyInstance)
        ?? throw new InvalidOperationException("notificationsStore field not found");
    static readonly MethodInfo OnCommittingMethod =
        typeof(ExpressPersistencePlugin).GetMethod("OnCommitting", AnyInstance)
        ?? throw new InvalidOperationException("OnCommitting not found");
    static readonly MethodInfo OnCommittedMethod =
        typeof(ExpressPersistencePlugin).GetMethod("OnCommitted", AnyInstance)
        ?? throw new InvalidOperationException("OnCommitted not found");
    static readonly FieldInfo EngineNotificationsField =
        typeof(ApplicationEngine).GetField("notifications", AnyInstance)
        ?? throw new InvalidOperationException("ApplicationEngine.notifications field not found");

    static readonly UInt160 NotifScriptHash = UInt160.Parse("0x8c23f196d8a1bfd841a4a3b9428ab48f01b3c23f");

    // ApplicationEngine.Create reads NativeContract.Ledger.CurrentIndex off its
    // snapshot, so the settings must come from a chain with a standby committee
    // (ProtocolSettings.Default cannot create a genesis block).
    static readonly Lazy<ProtocolSettings> Settings = new(() =>
    {
        using var stringReader = new StringReader(ChainJson);
        using var jsonReader = new JsonTextReader(stringReader);
        var chain = new Newtonsoft.Json.JsonSerializer().Deserialize<ExpressChain>(jsonReader)
            ?? throw new InvalidOperationException("chain deserialization failed");
        return chain.GetProtocolSettings();
    });

    [Fact]
    public void StageApplicationLogs_TxIndexCountsFaultTransactions()
    {
        var plugin = CreatePlugin();
        try
        {
            var faultTx = MakeTx(672); // transaction #0 of the block
            var haltTx = MakeTx(673);  // transaction #1 of the block
            var block = MakeBlock(42, faultTx, haltTx);

            var appExecs = new Blockchain.ApplicationExecuted[]
            {
                MakeAppExec(null, fault: false, notifCount: 0),     // block-level execution
                MakeAppExec(faultTx, fault: true, notifCount: 1),   // FAULT tx that notified before faulting
                MakeAppExec(haltTx, fault: false, notifCount: 1),   // HALT tx
            };

            InvokeOnCommitting(plugin, block, appExecs);
            InvokeOnCommitted(plugin, block);

            var notifications = plugin.GetNotifications(SeekDirection.Forward).ToList();

            // the FAULT tx's notification must not be surfaced by GetTransfers...
            notifications.Should().ContainSingle();
            notifications[0].notification.InventoryHash.Should().Be(haltTx.Hash);

            // ...and the HALT tx notification must carry the tx's real position
            // in block.Transactions (exposed verbatim as transfernotifyindex)
            notifications[0].txIndex.Should().Be((ushort)1);
        }
        finally
        {
            plugin.RemoveFromPluginList();
            plugin.Dispose();
        }
    }

    [Fact]
    public void StageApplicationLogs_TxIndexMatchesBlockPosition()
    {
        var plugin = CreatePlugin();
        try
        {
            var tx0 = MakeTx(674);
            var tx1 = MakeTx(675);
            var block = MakeBlock(43, tx0, tx1);

            var appExecs = new Blockchain.ApplicationExecuted[]
            {
                MakeAppExec(null, fault: false, notifCount: 0), // block-level execution
                MakeAppExec(tx0, fault: false, notifCount: 2),
                MakeAppExec(tx1, fault: false, notifCount: 2),
            };

            InvokeOnCommitting(plugin, block, appExecs);
            InvokeOnCommitted(plugin, block);

            var notifications = plugin.GetNotifications(SeekDirection.Forward).ToList();

            notifications.Should().HaveCount(4);
            notifications.Select(n => n.txIndex)
                .Should().Equal((ushort)0, (ushort)0, (ushort)1, (ushort)1);
            notifications.Select(n => n.notification.InventoryHash)
                .Should().Equal(tx0.Hash, tx0.Hash, tx1.Hash, tx1.Hash);
        }
        finally
        {
            plugin.RemoveFromPluginList();
            plugin.Dispose();
        }
    }

    static ExpressPersistencePlugin CreatePlugin()
    {
        var plugin = new ExpressPersistencePlugin();
        AppLogsStoreField.SetValue(plugin, new MemoryStore());
        NotificationsStoreField.SetValue(plugin, new MemoryStore());
        return plugin;
    }

    static void InvokeOnCommitting(ExpressPersistencePlugin plugin, Block block, IReadOnlyList<Blockchain.ApplicationExecuted> appExecs)
    {
        OnCommittingMethod.Invoke(plugin, new object?[] { null, block, null, appExecs })
            .Should().BeNull();
    }

    static void InvokeOnCommitted(ExpressPersistencePlugin plugin, Block block)
    {
        OnCommittedMethod.Invoke(plugin, new object?[] { null, block })
            .Should().BeNull();
    }

    static Transaction MakeTx(uint nonce)
        => new()
        {
            Nonce = nonce,
            SystemFee = 0,
            NetworkFee = 0,
            ValidUntilBlock = 2_100_000,
            Signers = new[] { new Signer { Account = UInt160.Zero, Scopes = WitnessScope.None } },
            Attributes = Array.Empty<TransactionAttribute>(),
            Script = new byte[] { (byte)OpCode.RET },
            Witnesses = Array.Empty<Witness>(),
        };

    static Block MakeBlock(uint index, params Transaction[] transactions)
        => new()
        {
            Header = new Header
            {
                Index = index,
                PrevHash = UInt256.Zero,
                MerkleRoot = MerkleTree.ComputeRoot(Array.Empty<UInt256>()),
                Timestamp = 1_000 + index,
                PrimaryIndex = 0,
                NextConsensus = UInt160.Zero,
                Witness = Witness.Empty,
            },
            Transactions = transactions,
        };

    static Blockchain.ApplicationExecuted MakeAppExec(Transaction? tx, bool fault, int notifCount)
    {
        var settings = Settings.Value;
        using var backing = new MemoryStore();
        backing.EnsureLedgerInitialized(settings);
        using var snapshot = new StoreCache(backing.GetSnapshot());
        using var engine = ApplicationEngine.Create(
            TriggerType.Application, tx, snapshot, null, settings, 2_000_000_000L);
        engine.LoadScript(new Script(
            fault ? new byte[] { (byte)OpCode.THROW } : new byte[] { (byte)OpCode.RET }));
        engine.Execute();
        engine.State.Should().Be(fault ? VMState.FAULT : VMState.HALT);

        if (notifCount > 0)
        {
            // a real contract can emit a Transfer notification and then fault;
            // attach real NotifyEventArgs instances to the engine via its private
            // notifications list so the internal ApplicationExecuted ctor picks them up
            var notifState = new Neo.VM.Types.Array(
                new Neo.VM.Types.StackItem[] { new Neo.VM.Types.Integer(1) });
            var notifications = new List<NotifyEventArgs>(notifCount);
            for (var i = 0; i < notifCount; i++)
            {
                notifications.Add(new NotifyEventArgs(tx, NotifScriptHash, "Transfer", notifState));
            }
            EngineNotificationsField.SetValue(engine, notifications);
        }

        return (Blockchain.ApplicationExecuted)(Activator.CreateInstance(
            typeof(Blockchain.ApplicationExecuted),
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object?[] { engine }, null)
            ?? throw new InvalidOperationException("ApplicationExecuted construction failed"));
    }

    const string ChainJson = @"{
  ""magic"": 5195086,
  ""address-version"": 53,
  ""consensus-nodes"": [
    { ""tcp-port"": 50012, ""rpc-port"": 40332,
      ""wallet"": { ""name"": ""Node1"", ""accounts"": [
        { ""private-key"": ""0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20"", ""script-hash"": ""0x8c23f196d8a1bfd841a4a3b9428ab48f01b3c23f"", ""label"": ""node1"", ""is-default"": true,
          ""contract"": { ""script"": ""QQ=="", ""parameters"": [""Signature""] } } ] } },
    { ""tcp-port"": 50112, ""rpc-port"": 40432,
      ""wallet"": { ""name"": ""Node2"", ""accounts"": [
        { ""private-key"": ""2101230405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f21"", ""script-hash"": ""0x0102030405060708091011121314151617181920"", ""label"": ""node2"", ""is-default"": true,
          ""contract"": { ""script"": ""QQ=="", ""parameters"": [""Signature""] } } ] } }
  ],
  ""wallets"": [],
  ""settings"": {
    ""chain.SecondsPerBlock"": ""15"",
    ""protocol.MillisecondsPerBlock"": ""15000"",
    ""protocol.MaxTransactionsPerBlock"": ""512"",
    ""protocol.MemoryPoolMaxTransactions"": ""50000"",
    ""protocol.MaxTraceableBlocks"": ""2102400"",
    ""protocol.MaxValidUntilBlockIncrement"": ""4320"",
    ""protocol.InitialGasDistribution"": ""5200000000000000"",
    ""protocol.Hardforks.HF_Echidna"": ""0"",
    ""protocol.Hardforks.HF_Faun"": ""100"",
    ""protocol.Hardforks.HF_Iguana"": ""100"",
    ""protocol.ValidatorsCount"": ""2""
  }
}";
}
