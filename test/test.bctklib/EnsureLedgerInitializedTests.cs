// Copyright (C) 2015-2026 The Neo Project.
//
// EnsureLedgerInitializedTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.SmartContract;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.SmartContract.Native;
using Xunit;

namespace test.bctklib
{
    public class EnsureLedgerInitializedTests
    {
        [Fact]
        public void persists_genesis_so_an_engine_can_read_the_current_index()
        {
            using var store = new MemoryStore();
            store.EnsureLedgerInitialized(DeployedContractFixture.Default);

            using var snapshot = new StoreCache(store.GetSnapshot());

            // Constructing an ApplicationEngine reads LedgerContract.CurrentIndex; before the fix the ledger
            // was never persisted, so this threw KeyNotFoundException.
            using var engine = new TestApplicationEngine(snapshot, DeployedContractFixture.Default);

            Assert.Equal(0u, NativeContract.Ledger.CurrentIndex(snapshot));
        }

        [Fact]
        public void is_idempotent()
        {
            using var store = new MemoryStore();
            store.EnsureLedgerInitialized(DeployedContractFixture.Default);
            // A second call must be a no-op (the ledger is already initialized), not a double-persist failure.
            store.EnsureLedgerInitialized(DeployedContractFixture.Default);

            using var snapshot = new StoreCache(store.GetSnapshot());
            Assert.Equal(0u, NativeContract.Ledger.CurrentIndex(snapshot));
        }
    }
}
