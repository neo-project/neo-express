// Copyright (C) 2015-2026 The Neo Project.
//
// ExpressWalletDefaultAccountTests.cs file belongs to neo-express project and is free
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
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using Xunit;

namespace test.bctklib;

// Regression tests for fuzzing-discovered crash classes in
// ExpressWallet.DefaultAccount (repro artifacts:
// artifacts/fuzz/crashes-round9/chain-1-min.json, chain-2-min.json and
// chain-2b-via-tryget.json). A malformed .neo-express file must never make
// the TryGet-style default-account lookups throw a raw NullReferenceException
// (null element in the accounts array) or an InvalidOperationException
// (more than one account flagged is-default).
public class ExpressWalletDefaultAccountTests
{
    const string FILENAME = "default.neo-express";
    const byte ADDRESS_VERSION = 53;
    const string PRIVATE_KEY = "0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20";

    static readonly UInt160 scriptHash = UInt160.Parse("0x8c23f196d8a1bfd841a4a3b9428ab48f01b3c23f");
    static readonly string address = scriptHash.ToAddress(ADDRESS_VERSION);

    static ExpressChain LoadChain(string json)
    {
        var fileSystem = new MockFileSystem();
        var fileName = fileSystem.Path.Combine(fileSystem.AllDirectories.First(), FILENAME);
        fileSystem.AddFile(fileName, new MockFileData(json));
        return fileSystem.LoadChain(fileName);
    }

    static string AccountJson(bool isDefault, string label)
        => $"{{'private-key': '{PRIVATE_KEY}', 'script-hash': '{address}', 'label': '{label}', 'is-default': {(isDefault ? "true" : "false")}}}";

    static string ChainJson(string accountsJson)
        => $"{{'magic': 42, 'address-version': {ADDRESS_VERSION}, 'wallets': [{{'name': 'genesis', 'accounts': [{accountsJson}]}}]}}";

    // chain-1-min.json: the wallets/genesis accounts array holds a null element
    // (a leading comma in the array deserializes the same way), so the
    // `a => a.IsDefault` predicate dereferenced null inside SingleOrDefault.
    [Theory]
    [InlineData("null")]
    [InlineData("null, null")]
    public void null_account_element_returns_false_from_TryGetDefaultAccount(string accountsJson)
    {
        var chain = LoadChain(ChainJson(accountsJson));

        Action action = () => chain.TryGetDefaultAccount("genesis", out _);

        action.Should().NotThrow();
        chain.TryGetDefaultAccount("genesis", out var account).Should().BeFalse();
        account.Should().BeNull();
        chain.GetWallet("genesis").DefaultAccount.Should().BeNull();
    }

    // chain-1.txt: a null element ahead of a valid default account made
    // GetDefaultAccountScriptHash throw NullReferenceException instead of
    // resolving the default account that follows the null entry.
    [Fact]
    public void null_account_element_before_default_account_does_not_throw_null_reference_exception()
    {
        var chain = LoadChain(ChainJson($"null, {AccountJson(true, "genesis")}"));

        Action action = () => chain.GetDefaultAccountScriptHash("genesis");

        action.Should().NotThrow<NullReferenceException>();
        chain.TryGetDefaultAccount("genesis", out var account).Should().BeTrue();
        account!.ScriptHash.Should().Be(address);
        chain.GetDefaultAccountScriptHash("genesis").Should().Be(scriptHash);
    }

    // chain-2b-via-tryget.json: two accounts in wallets/genesis both flagged
    // is-default made SingleOrDefault throw InvalidOperationException
    // ("Sequence contains more than one matching element"). TryGetDefaultAccount
    // is a TryGet method, so its contract is to return a value, not to throw.
    [Fact]
    public void multiple_default_accounts_do_not_throw_invalid_operation_exception()
    {
        var chain = LoadChain(ChainJson($"{AccountJson(true, "first")}, {AccountJson(true, "second")}"));

        Action tryGet = () => chain.TryGetDefaultAccount("genesis", out _);
        Action getScriptHash = () => chain.GetDefaultAccountScriptHash("genesis");

        tryGet.Should().NotThrow<InvalidOperationException>();
        getScriptHash.Should().NotThrow<InvalidOperationException>();
        chain.TryGetDefaultAccount("genesis", out var account).Should().BeTrue();
        account.Should().NotBeNull();
        chain.GetDefaultAccountScriptHash("genesis").Should().Be(scriptHash);
    }

    // chain-2-min.json: a consensus node wallet with two is-default accounts
    // (produced by a duplicated `wallet` key on the node) made
    // CreateGenesisContract throw InvalidOperationException.
    [Fact]
    public void multiple_default_accounts_in_consensus_node_wallet_do_not_throw_invalid_operation_exception()
    {
        var json = $"{{'magic': 42, 'address-version': {ADDRESS_VERSION}, 'consensus-nodes': [{{'tcp-port': 50012, 'rpc-port': 40332, 'wallet': {{'name': 'Node1', 'accounts': [{AccountJson(true, "node1")}, {AccountJson(true, "node1-duplicate")}]}}}}]}}";
        var chain = LoadChain(json);

        Action action = () => chain.CreateGenesisContract();

        action.Should().NotThrow<InvalidOperationException>();
        chain.ConsensusNodes[0].Wallet.DefaultAccount.Should().NotBeNull();
        chain.CreateGenesisContract().Should().NotBeNull();
    }

    // Accounts is declared non-nullable and its initializer survives a missing
    // "accounts" key, but Newtonsoft.Json assigns null to it for an explicit
    // "accounts": null (and for a missing value after the colon), so the lookup
    // has to tolerate a null list. Verified through IFileSystem.LoadChain and
    // through a direct JsonConvert.DeserializeObject<ExpressWallet>.
    [Theory]
    [InlineData("{'magic': 42, 'wallets': [{'name': 'genesis', 'accounts': null}]}")]
    [InlineData("{'magic': 42, 'wallets': [{'name': 'genesis', 'accounts':,}]}")]
    public void null_accounts_section_returns_false_from_TryGetDefaultAccount(string json)
    {
        var chain = LoadChain(json);

        Action action = () => chain.TryGetDefaultAccount("genesis", out _);

        action.Should().NotThrow();
        chain.TryGetDefaultAccount("genesis", out var account).Should().BeFalse();
        account.Should().BeNull();
    }

    [Fact]
    public void null_accounts_list_returns_null_default_account()
    {
        var chain = new ExpressChain
        {
            Wallets = new List<ExpressWallet> { new ExpressWallet { Name = "genesis", Accounts = null! } }
        };

        Func<ExpressWalletAccount?> getDefaultAccount = () => chain.GetWallet("genesis").DefaultAccount;

        getDefaultAccount.Should().NotThrow<ArgumentNullException>();
        chain.GetWallet("genesis").DefaultAccount.Should().BeNull();
        chain.TryGetDefaultAccount("genesis", out var account).Should().BeFalse();
        account.Should().BeNull();
    }

    [Fact]
    public void missing_accounts_key_keeps_the_empty_list_initializer()
    {
        var chain = LoadChain("{'magic': 42, 'wallets': [{'name': 'genesis'}]}");
        var wallet = chain.GetWallet("genesis");

        wallet.Accounts.Should().NotBeNull().And.BeEmpty();
        wallet.DefaultAccount.Should().BeNull();
        chain.TryGetDefaultAccount("genesis", out var account).Should().BeFalse();
        account.Should().BeNull();
    }

    // Behavior guard: a valid chain with exactly one default account must keep
    // resolving that account (FirstOrDefault == SingleOrDefault for one match).
    [Fact]
    public void valid_chain_resolves_single_default_account()
    {
        var chain = LoadChain(ChainJson($"{AccountJson(false, "watch-only")}, {AccountJson(true, "genesis")}"));

        chain.TryGetDefaultAccount("genesis", out var account).Should().BeTrue();
        account!.Label.Should().Be("genesis");
        account.IsDefault.Should().BeTrue();
        chain.GetDefaultAccount("genesis").Should().BeSameAs(account);
        chain.GetDefaultAccountScriptHash("genesis").Should().Be(scriptHash);
    }

    [Fact]
    public void valid_chain_without_default_account_returns_false_from_TryGetDefaultAccount()
    {
        var chain = LoadChain(ChainJson($"{AccountJson(false, "watch-only")}, {AccountJson(false, "second")}"));

        chain.TryGetDefaultAccount("genesis", out var account).Should().BeFalse();
        account.Should().BeNull();

        Action action = () => chain.GetDefaultAccount("genesis");

        action.Should().Throw<Exception>().WithMessage("*default account for genesis wallet not found*");
    }
}
