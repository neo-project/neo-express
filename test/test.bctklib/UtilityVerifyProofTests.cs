// Copyright (C) 2015-2026 The Neo Project.
//
// UtilityVerifyProofTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.Extensions;
using Neo.Persistence;
using Neo.Persistence.Providers;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace test.bctklib;

// Regression tests for a fuzzing-discovered crash class in
// Utility.VerifyProof (repro artifact:
// artifacts/fuzz/crashes-round10/proof-1-min.hex = "03675FFA"). The proof
// bytes arrive from untrusted sources — remote state service RPC responses
// (RpcClientExtensions.GetProvenState, StateServiceStore.ValidateFoundStates)
// and `neoxp show storage`/`show contract` output (NodeUtility) — so a
// truncated or corrupt proof must report the method's existing
// "Verification failed" error instead of escaping as a raw
// EndOfStreamException ("Unable to read beyond the end of the stream") or the
// MPTTrie library's InvalidOperationException ("Internal error, can't resolve
// hash when mpt get").
public class UtilityVerifyProofTests
{
    static readonly UInt256 rootHash = UInt256.Parse("0x7b70ffc4e8ef25290ece8bb4f4996730a98a5e1c4ca702b8de7f89ed9777a687");

    // A real proof for a real trie, so the fix is checked against the happy
    // path: key = storage key wire format (contract id little-endian + key
    // bytes), proofs serialized as VarBytes(key) + VarInt(count) + VarBytes.
    [Fact]
    public void valid_proof_round_trips()
    {
        const int contractId = 1;
        byte[] key = [0x01, 0x02, 0x03];
        byte[] value = [0xaa, 0xbb];

        var storageKey = new byte[sizeof(int) + key.Length];
        BinaryPrimitives.WriteInt32LittleEndian(storageKey, contractId);
        key.CopyTo(storageKey, sizeof(int));

        var snapshot = new MemoryStore().GetSnapshot();
        var trie = new Neo.Cryptography.MPTTrie.Trie(snapshot, null, false);
        trie.Put(storageKey, value);
        var root = trie.Root.Hash;
        trie.TryGetProof(storageKey, out var proofs);
        proofs.Should().NotBeEmpty();

        var proof = SerializeProofWire(storageKey, proofs);

        var (provenKey, provenValue) = Neo.BlockchainToolkit.Utility.VerifyProof(root, proof);

        provenKey.Id.Should().Be(contractId);
        provenKey.Key.Span.ToArray().Should().Equal(key);
        provenValue.Should().Equal(value);
    }

    // chain of truncation points: empty proof, length-only proof, varint
    // header announcing more entries than the stream contains
    // (artifacts/fuzz/crashes-round10/proof-1-min.hex = "03675FFA")
    [Theory]
    [InlineData("")]
    [InlineData("00")]
    [InlineData("03675FFA")]
    [InlineData("03675FFA01")]
    public void truncated_proof_reports_verification_failed(string proofHex)
    {
        var proof = proofHex.Length == 0 ? Array.Empty<byte>() : Convert.FromHexString(proofHex);

        var action = () => Neo.BlockchainToolkit.Utility.VerifyProof(rootHash, proof);

        // strictly the normalized System.Exception: a raw EndOfStreamException
        // (or any other subtype) escaping would fail BeOfType
        action.Should().Throw<Exception>().And
            .Should().BeOfType<Exception>()
            .Which.Message.Should().Be("Verification failed");
    }

    // A well-formed proof blob whose MPT nodes cannot resolve against the root
    // hash made MPTTrie throw a raw "Internal error" InvalidOperationException.
    [Fact]
    public void unresolvable_proof_reports_verification_failed()
    {
        var storageKey = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03 };

        var proof = SerializeProofWire(storageKey, [new byte[] { 0x01, 0xaa, 0xbb }]);

        var action = () => Neo.BlockchainToolkit.Utility.VerifyProof(rootHash, proof);

        action.Should().Throw<Exception>().And
            .Should().BeOfType<Exception>()
            .Which.Message.Should().Be("Verification failed");
    }

    [Fact]
    public void null_proof_throws_argument_null()
    {
        var action = () => Neo.BlockchainToolkit.Utility.VerifyProof(rootHash, null);

        action.Should().Throw<ArgumentNullException>();
    }

    static byte[] SerializeProofWire(byte[] storageKey, IReadOnlyCollection<byte[]> proofs)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Neo.Utility.StrictUTF8);
        writer.WriteVarBytes(storageKey);
        writer.WriteVarInt(proofs.Count);
        foreach (var proof in proofs)
        {
            writer.WriteVarBytes(proof);
        }
        writer.Flush();
        return ms.ToArray();
    }
}
