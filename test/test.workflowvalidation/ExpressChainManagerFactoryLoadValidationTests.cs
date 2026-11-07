// Copyright (C) 2015-2026 The Neo Project.
//
// ExpressChainManagerFactoryLoadValidationTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using NeoExpress;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using Xunit;

namespace test.workflowvalidation;

// Regression tests for a fuzzing-discovered crash class in
// ExpressChainManagerFactory.LoadChain (repro artifact:
// artifacts/fuzz/crashes-round10/chain-1-min.json). Newtonsoft.Json
// deserializes explicit null sections from a malformed .neo-express file into
// null lists/null references ("consensus-nodes": null, "wallets": null,
// "wallet": null, null elements inside consensus-nodes), and LoadChain
// dereferenced them without guards — so every neoxp command crashed with a raw
// NullReferenceException instead of the file-validation errors it throws for
// other malformed content.
public class ExpressChainManagerFactoryLoadValidationTests
{
    const string FILENAME = "default.neo-express";

    static ExpressChainManagerFactory CreateFactory(string json)
    {
        var fileSystem = new MockFileSystem();
        var fileName = fileSystem.Path.Combine(fileSystem.AllDirectories.First(), FILENAME);
        fileSystem.AddFile(fileName, new MockFileData($"{{{json}}}"));
        return new ExpressChainManagerFactory(fileSystem);
    }

    // "consensus-nodes": null deserialized into a null ConsensusNodes list, so
    // the existing `chain.ConsensusNodes.Count == 0` check threw
    // NullReferenceException before reaching its clean file-validation error.
    [Theory]
    [InlineData("'magic': 42, 'consensus-nodes': null")]
    [InlineData("'magic': 42, 'consensus-nodes':,")]
    public void null_consensus_nodes_section_reports_file_validation_error(string json)
    {
        var factory = CreateFactory(json);

        var action = () => factory.LoadChain(FILENAME);

        action.Should().Throw<Exception>()
            .WithMessage("*is not a valid neo-express file: it has no consensus nodes*");
    }

    // A null element inside the consensus-nodes array (e.g. from a hand-edited
    // file) reached `chain.ConsensusNodes[0].Wallet` and threw
    // NullReferenceException.
    [Fact]
    public void null_consensus_node_element_reports_file_validation_error()
    {
        var factory = CreateFactory("'magic': 42, 'consensus-nodes': [null]");

        var action = () => factory.LoadChain(FILENAME);

        action.Should().Throw<Exception>()
            .WithMessage("*is not a valid neo-express file: it has no consensus nodes*");
    }

    // "wallet": null deserialized into a null ExpressConsensusNode.Wallet, so
    // `chain.ConsensusNodes[0].Wallet.DefaultAccount` threw
    // NullReferenceException.
    [Fact]
    public void null_node_wallet_reports_missing_default_account()
    {
        var factory = CreateFactory(
            "'magic': 42, 'consensus-nodes': [{'tcp-port': 50012, 'rpc-port': 40332, 'wallet': null}]");

        var action = () => factory.LoadChain(FILENAME);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*consensus node 0 missing default account*");
    }

    // A consensus node without a wallet section kept its pre-existing
    // InvalidOperationException (an ExpressWallet instance with no accounts).
    [Fact]
    public void missing_node_wallet_reports_missing_default_account()
    {
        var factory = CreateFactory("'magic': 42, 'consensus-nodes': [{'tcp-port': 50012, 'rpc-port': 40332}]");

        var action = () => factory.LoadChain(FILENAME);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*consensus node 0 missing default account*");
    }

    // "wallets": null passed the consensus-node validation and crashed later
    // commands (e.g. `neoxp wallet list`'s foreach over chain.Wallets) with a
    // raw NullReferenceException.
    [Fact]
    public void null_wallets_section_reports_file_validation_error()
    {
        var factory = CreateFactory(
            "'magic': 42, 'consensus-nodes': [{'tcp-port': 50012, 'rpc-port': 40332, 'wallet': {'name': 'Node1', 'accounts': []}}], 'wallets': null");

        var action = () => factory.LoadChain(FILENAME);

        action.Should().Throw<Exception>()
            .WithMessage("*is not a valid neo-express file: its wallets section is missing*");
    }
}
