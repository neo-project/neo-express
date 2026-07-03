// Copyright (C) 2015-2026 The Neo Project.
//
// StateServiceStoreBranchInfoTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit.Persistence;
using Neo.Extensions;
using Neo.Json;
using Neo.Network.RPC;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace test.bctklib;

public class StateServiceStoreBranchInfoTests
{
    [Fact]
    public async Task GetContractHashesAsync_reads_the_lightweight_contract_hash_index()
    {
        const int contractId = 42;
        var contractHash = UInt160.Parse("0x1111111111111111111111111111111111111111");
        var contractKey = CreateContractHashKey(contractId);
        var contractValue = contractHash.ToArray();
        var nativeKey = CreateContractHashKey(NativeContract.ContractManagement.Id);
        var nativeValue = NativeContract.ContractManagement.Hash.ToArray();

        using var proofStore = new MemoryStore();
        using var snapshot = proofStore.GetSnapshot();
        var trie = new Neo.Cryptography.MPTTrie.Trie(snapshot, null);
        var storageKey = new StorageKey { Id = NativeContract.ContractManagement.Id, Key = contractKey }.ToArray();
        var nativeStorageKey = new StorageKey { Id = NativeContract.ContractManagement.Id, Key = nativeKey }.ToArray();
        trie.Put(storageKey, contractValue);
        trie.Put(nativeStorageKey, nativeValue);
        trie.Commit();
        snapshot.Commit();

        var proof = Convert.ToBase64String(trie.GetSerializedProof(storageKey));
        var nativeProof = Convert.ToBase64String(trie.GetSerializedProof(nativeStorageKey));
        var rpcClient = new RecordingRpcClient((method, _) =>
            method == "findstates"
                ? new JObject
                {
                    ["firstProof"] = proof,
                    ["lastProof"] = nativeProof,
                    ["truncated"] = false,
                    ["results"] = new JArray
                    {
                        new JObject
                        {
                            ["key"] = Convert.ToBase64String(contractKey),
                            ["value"] = Convert.ToBase64String(contractValue)
                        },
                        new JObject
                        {
                            ["key"] = Convert.ToBase64String(nativeKey),
                            ["value"] = Convert.ToBase64String(nativeValue)
                        }
                    }
                }
                : throw new InvalidOperationException($"{method} should not be called"));

        var contracts = await StateServiceStore.GetContractHashesAsync(rpcClient, trie.Root.Hash);

        contracts.Should().Contain(c =>
            c.Id == NativeContract.ContractManagement.Id
            && c.Hash == NativeContract.ContractManagement.Hash
            && c.Name == NativeContract.ContractManagement.Name);
        contracts.Should().Contain(c =>
            c.Id == contractId
            && c.Hash == contractHash
            && c.Name == contractHash.ToString());
        contracts.Count(c => c.Id == NativeContract.ContractManagement.Id).Should().Be(1);
        rpcClient.Methods.Should().ContainSingle("findstates");
    }

    static byte[] CreateContractHashKey(int contractId)
    {
        var key = new byte[5];
        key[0] = 0x0C;
        BinaryPrimitives.WriteInt32BigEndian(key.AsSpan(1), contractId);
        return key;
    }

    sealed class RecordingRpcClient : RpcClient
    {
        readonly Func<string, JToken[], JToken> handler;

        public RecordingRpcClient(Func<string, JToken[], JToken> handler) : base(null!)
        {
            this.handler = handler;
        }

        public List<string> Methods { get; } = new();

        public override Task<JToken> RpcSendAsync(string method, params JToken[] paraArgs)
        {
            Methods.Add(method);
            return Task.FromResult(handler(method, paraArgs));
        }
    }
}
