// Copyright (C) 2015-2026 The Neo Project.
//
// CreateEngineTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Persistence;
using Neo.Cryptography.ECC;
using Neo.Network.P2P.Payloads;
using Neo.VM;
using Neo.Wallets;
using NeoTestHarness;
using Xunit;

namespace test.harness;

public class CreateEngineTests : IDisposable
{
    const uint NETWORK = 0x746E7534;
    const byte ADDRESS_VERSION = 53;

    readonly string dbPath;
    readonly string checkpointPath;
    readonly TestFixture fixture;

    public CreateEngineTests()
    {
        var root = Path.Combine(Path.GetTempPath(), $"neo-express-create-engine-{Guid.NewGuid():N}");
        dbPath = Path.Combine(root, "db");
        checkpointPath = Path.Combine(root, "test.neoxp-checkpoint");
        Directory.CreateDirectory(dbPath);

        using (var db = RocksDbUtility.OpenDb(dbPath))
        {
            // the engine reads ledger state, so persist the genesis block before checkpointing
            var key = new KeyPair(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());
            var settings = ProtocolSettings.Default with
            {
                Network = NETWORK,
                AddressVersion = ADDRESS_VERSION,
                ValidatorsCount = 1,
                StandbyCommittee = new ECPoint[] { key.PublicKey },
            };
            using (var store = new RocksDbStore(db, readOnly: false, shared: true))
            {
                store.EnsureLedgerInitialized(settings);
            }
            RocksDbUtility.CreateCheckpoint(db, checkpointPath, NETWORK, ADDRESS_VERSION, UInt160.Zero);
        }

        fixture = new TestFixture(checkpointPath);
    }

    public void Dispose()
    {
        fixture.Dispose();
        var root = Path.GetDirectoryName(checkpointPath)!;
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    [Fact]
    public void create_engine_builds_a_working_engine_with_the_fixture_settings()
    {
        using var engine = fixture.CreateEngine();

        engine.ProtocolSettings.Network.Should().Be(NETWORK);
        engine.ProtocolSettings.AddressVersion.Should().Be(ADDRESS_VERSION);

        engine.ExecuteScript(new Script(new[] { (byte)OpCode.PUSH1 })).Should().Be(VMState.HALT);
        engine.ResultStack.Should().HaveCount(1);
    }

    [Fact]
    public void create_engine_applies_the_requested_signer()
    {
        var signer = UInt160.Parse("0x0000000000000000000000000000000000000001");

        using var engine = fixture.CreateEngine(signer, WitnessScope.Global);

        var tx = engine.ScriptContainer.Should().BeOfType<Transaction>().Which;
        tx.Signers.Should().ContainSingle().Which.Account.Should().Be(signer);
        tx.Signers[0].Scopes.Should().Be(WitnessScope.Global);
    }

    [Fact]
    public void engines_are_independent_and_dispose_cleanly()
    {
        var first = fixture.CreateEngine();
        first.Dispose();
        first.Dispose(); // double dispose is safe

        using var second = fixture.CreateEngine();
        second.ExecuteScript(new Script(new[] { (byte)OpCode.PUSH1 })).Should().Be(VMState.HALT);
    }

    sealed class TestFixture : CheckpointFixture
    {
        public TestFixture(string checkpointPath) : base(checkpointPath)
        {
        }
    }
}
