// Copyright (C) 2015-2026 The Neo Project.
//
// BlockProducerTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit;
using Neo.Cryptography.ECC;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.SmartContract.Native;
using Neo.Wallets;
using System;
using System.Linq;
using Xunit;

namespace test.bctklib
{
    public class BlockProducerTests
    {
        static readonly KeyPair consensusKey = new(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());

        static ProtocolSettings Settings => ProtocolSettings.Default with
        {
            Network = 0x746E7534,
            ValidatorsCount = 1,
            StandbyCommittee = new ECPoint[] { consensusKey.PublicKey },
            SeedList = Array.Empty<string>(),
        };

        [Fact]
        public void fast_forward_appends_the_requested_blocks_with_the_timestamp_delta()
        {
            var settings = Settings;
            using var store = new MemoryStore();
            store.EnsureLedgerInitialized(settings);

            BlockProducer.FastForward(store, 3, TimeSpan.FromHours(1), new[] { consensusKey }, settings);

            using var snapshot = new StoreCache(store.GetSnapshot());
            NativeContract.Ledger.CurrentIndex(snapshot).Should().Be(3);

            // walk the chain backwards, asserting the headers link and are signed
            var header = NativeContract.Ledger.GetHeader(snapshot, NativeContract.Ledger.CurrentHash(snapshot));
            var last = header;
            for (var index = 3u; index >= 1; index--)
            {
                header.Index.Should().Be(index);
                header.Witness.InvocationScript.Length.Should().BeGreaterThan(0);
                header = NativeContract.Ledger.GetHeader(snapshot, header.PrevHash);
            }
            header.Index.Should().Be(0); // genesis

            // the last block's timestamp is the first new block's timestamp plus the delta
            var first = NativeContract.Ledger.GetHeader(snapshot, NativeContract.Ledger.GetBlockHash(snapshot, 1));
            (last.Timestamp - first.Timestamp).Should().Be((ulong)TimeSpan.FromHours(1).TotalMilliseconds);
        }

        [Fact]
        public void fast_forward_zero_blocks_is_a_no_op()
        {
            var settings = Settings;
            using var store = new MemoryStore();
            store.EnsureLedgerInitialized(settings);

            BlockProducer.FastForward(store, 0, TimeSpan.Zero, new[] { consensusKey }, settings);

            using var snapshot = new StoreCache(store.GetSnapshot());
            NativeContract.Ledger.CurrentIndex(snapshot).Should().Be(0);
        }

        [Fact]
        public void fast_forward_rejects_a_negative_timestamp_delta()
        {
            var settings = Settings;
            using var store = new MemoryStore();
            store.EnsureLedgerInitialized(settings);

            var act = () => BlockProducer.FastForward(store, 1, TimeSpan.FromSeconds(-1), new[] { consensusKey }, settings);

            act.Should().Throw<ArgumentException>();
        }
    }
}
