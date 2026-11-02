// Copyright (C) 2015-2026 The Neo Project.
//
// GetProtocolSettingsValidationTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using System;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using Xunit;

namespace test.bctklib;

// Regression tests for fuzzing-discovered crash classes in the
// ExpressChainExtensions.GetProtocolSettings path (repro artifacts:
// artifacts/fuzz/crashes-round2/chain-1.txt through chain-4.txt).
// Malformed consensus-node wallet/protocol sections must produce clean
// ArgumentException validation errors, not InvalidOperationException
// (unguarded .Single()) or NullReferenceException.
public class GetProtocolSettingsValidationTests
{
    const string FILENAME = "default.neo-express";
    const string NODE1_PRIVATE_KEY = "0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20";
    const string NODE1_SCRIPT_HASH = "0x8c23f196d8a1bfd841a4a3b9428ab48f01b3c23f";

    static ExpressChain LoadChain(string json)
    {
        var fileSystem = new MockFileSystem();
        var fileName = fileSystem.Path.Combine(fileSystem.AllDirectories.First(), FILENAME);
        fileSystem.AddFile(fileName, new MockFileData(json));
        return fileSystem.LoadChain(fileName);
    }

    // chain-1.txt: consensus node wallet with no private-key entries (wallet
    // section missing or accounts empty) — .Single() threw InvalidOperationException
    // ("Sequence contains no elements") in GetPublicKey.
    [Theory]
    [InlineData("{'magic': 42, 'consensus-nodes': [{'tcp-port': 50012, 'rpc-port': 40332, 'wallet': {'name': 'Node1', 'accounts': []}}]}")]
    [InlineData("{'magic': 42, 'consensus-nodes': [{'tcp-port': 50012, 'rpc-port': 40332}]}")]
    public void consensus_node_wallet_without_private_keys_reports_validation_error(string json)
    {
        var chain = LoadChain(json);

        var action = () => chain.GetProtocolSettings();

        action.Should().Throw<ArgumentException>().WithMessage("*consensus node wallet*");
    }

    // chain-2.txt: malformed consensus-nodes section (value missing after the
    // colon deserializes as null) — NullReferenceException on ConsensusNodes.Count.
    [Theory]
    [InlineData("{'magic': 42, 'consensus-nodes': null}")]
    [InlineData("{'magic': 42, 'consensus-nodes':,}")]
    public void null_consensus_nodes_reports_validation_error(string json)
    {
        var chain = LoadChain(json);

        var action = () => chain.GetProtocolSettings();

        action.Should().Throw<ArgumentException>().WithMessage("*consensus-nodes*");
    }

    // chain-3.txt: consensus node wallet accounts section containing a null
    // entry — NullReferenceException in the a => a.PrivateKey lambda of GetPublicKey.
    [Fact]
    public void consensus_node_wallet_null_account_entry_reports_validation_error()
    {
        var chain = LoadChain($"{{'magic': 42, 'consensus-nodes': [{{'tcp-port': 50012, 'rpc-port': 40332, 'wallet': {{'name': 'Node1', 'accounts': [null, {{'private-key': '{NODE1_PRIVATE_KEY}', 'script-hash': '{NODE1_SCRIPT_HASH}', 'label': 'node1', 'is-default': true}}]}}}}]}}");

        var action = () => chain.GetProtocolSettings();

        action.Should().Throw<ArgumentException>().WithMessage("*consensus node wallet*");
    }

    // chain-4.txt: consensus node with a null wallet section —
    // NullReferenceException on node.Wallet.Accounts in GetPublicKey.
    [Fact]
    public void null_consensus_node_wallet_reports_validation_error()
    {
        var chain = LoadChain("{'magic': 42, 'consensus-nodes': [{'tcp-port': 50012, 'rpc-port': 40332, 'wallet': null}]}");

        var action = () => chain.GetProtocolSettings();

        action.Should().Throw<ArgumentException>().WithMessage("*consensus node wallet*");
    }
}
