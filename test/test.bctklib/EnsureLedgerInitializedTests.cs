// Copyright (C) 2015-2026 The Neo Project.
//
// EnsureLedgerInitializedTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Neo.BlockchainToolkit.SmartContract;
using Neo.Extensions;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.SmartContract.Native;
using Neo.Wallets;
using System;
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

        [Fact]
        public void null_checkpoint_store_can_seed_memory_tracking_store()
        {
            var chain = CreateExpressChain();
            ICheckpointStore checkpoint = new NullCheckpointStore(chain);
            var readOnlyStore = Assert.IsAssignableFrom<IReadOnlyStore<byte[], byte[]>>(checkpoint);
            using var store = new MemoryTrackingStore(readOnlyStore);

            Assert.Equal(chain.Network, checkpoint.Settings.Network);
            Assert.Equal(chain.AddressVersion, checkpoint.Settings.AddressVersion);
            Assert.Equal(chain.ConsensusNodes.Count, checkpoint.Settings.ValidatorsCount);
            Assert.Equal(chain.GetProtocolSettings().StandbyCommittee, checkpoint.Settings.StandbyCommittee);

            store.EnsureLedgerInitialized(checkpoint.Settings);

            using var snapshot = new StoreCache(store.GetSnapshot());
            Assert.Equal(0u, NativeContract.Ledger.CurrentIndex(snapshot));
        }

        private static ExpressChain CreateExpressChain()
        {
            var key = new KeyPair(Convert.FromHexString("2A79CFA210832EB7139C36168E415DC78E2649EA7735060BF1DAE04C05050A98"));
            return new ExpressChain
            {
                Network = 0x334F454Eu,
                AddressVersion = ProtocolSettings.Default.AddressVersion,
                ConsensusNodes =
                [
                    new ExpressConsensusNode
                    {
                        TcpPort = 50013,
                        RpcPort = 50012,
                        Wallet = new ExpressWallet
                        {
                            Name = "node1",
                            Accounts =
                            [
                                new ExpressWalletAccount
                                {
                                    PrivateKey = key.PrivateKey.ToHexString(),
                                    IsDefault = true,
                                },
                            ],
                        },
                    },
                ],
            };
        }
    }
}
