// Copyright (C) 2015-2026 The Neo Project.
//
// StateServiceStoreExactReadTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Neo.Json;
using Neo.Network.RPC;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using System;
using System.Collections.Generic;
using Xunit;

namespace test.bctklib;

public class StateServiceStoreExactReadTests
{
    [Fact]
    public void TryGet_for_deployed_contract_uses_proven_state_without_prefetching_all_states()
    {
        var contractId = 1;
        var contractHash = UInt160.Parse("0x1111111111111111111111111111111111111111");
        var contractKey = new byte[] { 0x01, 0x02 };
        var expected = new byte[] { 0x03, 0x04 };

        var store = CreateStateServiceStore(contractId, contractHash, "SampleContract", contractKey, expected, out var rpcClient);
        var fullKey = new StorageKey { Id = contractId, Key = contractKey }.ToArray();

        store.TryGet(fullKey, out var actual).Should().BeTrue();

        actual.Should().Equal(expected);
        rpcClient.Methods.Should().ContainSingle("getproof");
    }

    [Fact]
    public void TryGet_for_seekable_native_contract_uses_proven_state_without_prefetching_all_states()
    {
        var contractId = NativeContract.ContractManagement.Id;
        var contractHash = NativeContract.ContractManagement.Hash;
        var contractKey = new byte[] { 0x08, 0x01 };
        var expected = new byte[] { 0x05, 0x06 };

        var store = CreateStateServiceStore(contractId, contractHash, "ContractManagement", contractKey, expected, out var rpcClient);
        var fullKey = new StorageKey { Id = contractId, Key = contractKey }.ToArray();

        store.TryGet(fullKey, out var actual).Should().BeTrue();

        actual.Should().Equal(expected);
        rpcClient.Methods.Should().ContainSingle("getproof");
    }

    [Fact]
    public void Find_supports_policy_blocked_account_prefix()
    {
        var rpcClient = new RecordingRpcClient((method, _) =>
            method == "findstates"
                ? new JObject
                {
                    ["firstProof"] = null,
                    ["lastProof"] = null,
                    ["truncated"] = false,
                    ["results"] = new JArray()
                }
                : throw new InvalidOperationException($"{method} should not be called for PolicyContract seek"));
        var branchInfo = new BranchInfo(
            ProtocolSettings.Default.Network,
            ProtocolSettings.Default.AddressVersion,
            1,
            UInt256.Zero,
            UInt256.Zero,
            new[] { new ContractInfo(NativeContract.Policy.Id, NativeContract.Policy.Hash, "PolicyContract") });
        using var store = new StateServiceStore(rpcClient, branchInfo);
        var blockedAccountPrefix = StorageKey.CreateSearchPrefix(NativeContract.Policy.Id, new byte[] { 0x0F });

        store.Find(blockedAccountPrefix).Should().BeEmpty();

        rpcClient.Methods.Should().ContainSingle("findstates");
    }

    static StateServiceStore CreateStateServiceStore(
        int contractId,
        UInt160 contractHash,
        string contractName,
        byte[] contractKey,
        byte[] value,
        out RecordingRpcClient rpcClient)
    {
        using var proofStore = new MemoryStore();
        using var snapshot = proofStore.GetSnapshot();
        var trie = new Neo.Cryptography.MPTTrie.Trie(snapshot, null);
        var fullKey = new StorageKey { Id = contractId, Key = contractKey }.ToArray();
        trie.Put(fullKey, value);
        trie.Commit();
        snapshot.Commit();

        var proof = Convert.ToBase64String(trie.GetSerializedProof(fullKey));
        rpcClient = new RecordingRpcClient((method, _) =>
            method == "getproof"
                ? new JString(proof)
                : throw new InvalidOperationException($"{method} should not be called for exact reads"));

        var branchInfo = new BranchInfo(
            ProtocolSettings.Default.Network,
            ProtocolSettings.Default.AddressVersion,
            1,
            UInt256.Zero,
            trie.Root.Hash,
            new[] { new ContractInfo(contractId, contractHash, contractName) });

        return new StateServiceStore(rpcClient, branchInfo);
    }

    sealed class RecordingRpcClient : RpcClient
    {
        readonly Func<string, JToken[], JToken> handler;

        public RecordingRpcClient(Func<string, JToken[], JToken> handler) : base(null!)
        {
            this.handler = handler;
        }

        public List<string> Methods { get; } = new();

        public override JToken RpcSend(string method, params JToken[] paraArgs)
        {
            Methods.Add(method);
            return handler(method, paraArgs);
        }
    }
}
