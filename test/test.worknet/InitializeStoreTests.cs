// Copyright (C) 2015-2026 The Neo Project.
//
// InitializeStoreTests.cs file belongs to neo-express project and is free
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
using Neo.Cryptography.ECC;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.SmartContract.Native;
using Neo.Wallets;
using NeoWorkNet.Commands;
using System.Linq;
using System.Numerics;
using Xunit;

namespace test.worknet;

public class InitializeStoreTests
{
    static readonly KeyPair genesisKey = new(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());

    static ProtocolSettings Settings => ProtocolSettings.Default with
    {
        Network = 0x746E7535,
        ValidatorsCount = 1,
        StandbyCommittee = new ECPoint[] { genesisKey.PublicKey },
        SeedList = System.Array.Empty<string>(),
    };

    static (MemoryStore store, WalletAccount account) CreateInitializedStore()
    {
        var settings = Settings;
        var store = new MemoryStore();
        store.EnsureLedgerInitialized(settings);

        var wallet = new ToolkitWallet("consensus", settings);
        var account = wallet.CreateAccount();
        account.IsDefault = true;
        return (store, account);
    }

    [Fact]
    public void initialize_store_seeds_the_consensus_account_with_gas()
    {
        var (store, account) = CreateInitializedStore();
        using var _ = store;

        BigInteger supplyBefore;
        BigInteger sourceBalanceBefore;
        var sourceAccount = Neo.SmartContract.Contract.GetBFTAddress(Settings.StandbyValidators);
        using (var snapshot = new StoreCache(store.GetSnapshot()))
        {
            supplyBefore = NativeContract.GAS.TotalSupply(snapshot);
            sourceBalanceBefore = NativeContract.GAS.BalanceOf(snapshot, sourceAccount);
        }

        CreateCommand.InitializeStore(store, CreateCommand.DEFAULT_GAS_SEED, new[] { account }, Settings);

        using var after = new StoreCache(store.GetSnapshot());
        var expected = new BigInteger(10_000) * 100_000_000; // default seed, GAS has 8 decimals
        NativeContract.GAS.BalanceOf(after, account.ScriptHash).Should().Be(expected);
        NativeContract.GAS.BalanceOf(after, sourceAccount).Should().Be(sourceBalanceBefore - expected);
        NativeContract.GAS.TotalSupply(after).Should().Be(supplyBefore);
    }

    [Fact]
    public void initialize_store_honors_a_custom_gas_seed()
    {
        var (store, account) = CreateInitializedStore();
        using var _ = store;

        CreateCommand.InitializeStore(store, 12.5m, new[] { account }, Settings);

        using var after = new StoreCache(store.GetSnapshot());
        NativeContract.GAS.BalanceOf(after, account.ScriptHash).Should().Be(new BigInteger(1_250_000_000));
    }

    [Fact]
    public void initialize_store_seeds_each_consensus_account_with_gas()
    {
        var (store, account1) = CreateInitializedStore();
        using var _ = store;

        var wallet = new ToolkitWallet("consensus", Settings);
        var account2 = wallet.CreateAccount();
        var accounts = new[] { account1, account2 };
        var sourceAccount = Neo.SmartContract.Contract.GetBFTAddress(Settings.StandbyValidators);

        BigInteger supplyBefore;
        BigInteger sourceBalanceBefore;
        using (var snapshot = new StoreCache(store.GetSnapshot()))
        {
            supplyBefore = NativeContract.GAS.TotalSupply(snapshot);
            sourceBalanceBefore = NativeContract.GAS.BalanceOf(snapshot, sourceAccount);
        }

        CreateCommand.InitializeStore(store, 2.5m, accounts, Settings);

        using var after = new StoreCache(store.GetSnapshot());
        var expectedPerAccount = new BigInteger(250_000_000);
        NativeContract.GAS.BalanceOf(after, account1.ScriptHash).Should().Be(expectedPerAccount);
        NativeContract.GAS.BalanceOf(after, account2.ScriptHash).Should().Be(expectedPerAccount);
        NativeContract.GAS.BalanceOf(after, sourceAccount).Should().Be(sourceBalanceBefore - (expectedPerAccount * accounts.Length));
        NativeContract.GAS.TotalSupply(after).Should().Be(supplyBefore);
    }

    [Fact]
    public void initialize_store_seeds_nothing_when_gas_is_zero()
    {
        var (store, account) = CreateInitializedStore();
        using var _ = store;

        BigInteger supplyBefore;
        using (var snapshot = new StoreCache(store.GetSnapshot()))
        {
            supplyBefore = NativeContract.GAS.TotalSupply(snapshot);
        }

        CreateCommand.InitializeStore(store, 0m, new[] { account }, Settings);

        using var after = new StoreCache(store.GetSnapshot());
        NativeContract.GAS.BalanceOf(after, account.ScriptHash).Should().Be(BigInteger.Zero);
        NativeContract.GAS.TotalSupply(after).Should().Be(supplyBefore);
    }

    [Fact]
    public void initialize_store_still_replaces_the_committee_and_appends_the_branch_block()
    {
        var (store, account) = CreateInitializedStore();
        using var _ = store;

        CreateCommand.InitializeStore(store, CreateCommand.DEFAULT_GAS_SEED, new[] { account }, Settings);

        using var after = new StoreCache(store.GetSnapshot());
        // the branch block was appended on top of genesis
        NativeContract.Ledger.CurrentIndex(after).Should().Be(1);
        // the committee was replaced with the worknet consensus account's key
        var committee = NativeContract.NEO.GetCommitteeAddress(after);
        var expected = Neo.SmartContract.Contract.CreateMultiSigContract(1, new[] { account.GetKey()!.PublicKey }).ScriptHash;
        committee.Should().Be(expected);
    }
}
