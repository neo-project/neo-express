// Copyright (C) 2015-2026 The Neo Project.
//
// NeoStorageExtensionsTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit.Utilities;
using Neo.Extensions;
using Neo.SmartContract;
using NeoTestHarness;
using System.Text;
using Xunit;

namespace test.harness;

public class NeoStorageExtensionsTests
{
    [Fact]
    public void StorageMap_byte_prefix_returns_matching_entries_with_prefix_stripped()
    {
        var storages = BuildStorage(
            ([0x01, 0xaa], [0x11]),
            ([0x01, 0xbb], [0x22]),
            ([0x02, 0xcc], [0x33]));

        var mapped = storages.StorageMap((byte)0x01);

        mapped.Should().HaveCount(2);
        mapped.TryGetValue(new ReadOnlyMemory<byte>([0xaa]), out var first).Should().BeTrue();
        first!.Value.ToArray().Should().Equal(0x11);
        mapped.TryGetValue(new ReadOnlyMemory<byte>([0xbb]), out var second).Should().BeTrue();
        second!.Value.ToArray().Should().Equal(0x22);
    }

    [Fact]
    public void StorageMap_string_prefix_uses_utf8_bytes()
    {
        var prefix = "kv:"u8.ToArray();
        var storages = BuildStorage(
            (Concat(prefix, [0x01]), [0xaa]),
            (Concat(prefix, [0x02]), [0xbb]),
            ([0x00], [0xcc]));

        var mapped = storages.StorageMap("kv:");

        mapped.Should().HaveCount(2);
        mapped.TryGetValue(new ReadOnlyMemory<byte>([0x01]), out var first).Should().BeTrue();
        first!.Value.ToArray().Should().Equal(0xaa);
        mapped.TryGetValue(new ReadOnlyMemory<byte>([0x02]), out var second).Should().BeTrue();
        second!.Value.ToArray().Should().Equal(0xbb);
    }

    [Fact]
    public void StorageMap_UInt160_prefix_strips_prefix()
    {
        var hash = UInt160.Parse("0x0102030405060708090a0b0c0d0e0f1011121314");
        var hashBytes = hash.ToArray();
        var storages = BuildStorage(
            (Concat(hashBytes, [0xaa]), [0x11]),
            (Concat(hashBytes, [0xbb]), [0x22]),
            ([0x99], [0x33]));

        var mapped = storages.StorageMap(hash);

        mapped.Should().HaveCount(2);
        mapped.TryGetValue(new ReadOnlyMemory<byte>([0xaa]), out var first).Should().BeTrue();
        first!.Value.ToArray().Should().Equal(0x11);
        mapped.TryGetValue(new ReadOnlyMemory<byte>([0xbb]), out var second).Should().BeTrue();
        second!.Value.ToArray().Should().Equal(0x22);
    }

    [Fact]
    public void StorageMap_UInt256_prefix_strips_prefix()
    {
        var hash = UInt256.Parse("0x0101010101010101010101010101010101010101010101010101010101010101");
        var hashBytes = hash.ToArray();
        var storages = BuildStorage(
            (Concat(hashBytes, [0x01]), [0xcd]),
            ([0x99], [0x33]));

        var mapped = storages.StorageMap(hash);

        mapped.Should().HaveCount(1);
        mapped.TryGetValue(new ReadOnlyMemory<byte>([0x01]), out var item).Should().BeTrue();
        item!.Value.ToArray().Should().Equal(0xcd);
    }

    [Fact]
    public void TryGetValue_byte_key_returns_matching_entry()
    {
        var storages = BuildStorage(
            ([0x05], [0xff]),
            ([0x06], [0xee]));

        storages.TryGetValue((byte)0x05, out var item).Should().BeTrue();
        item!.Value.ToArray().Should().Equal(0xff);

        storages.TryGetValue((byte)0x09, out var missing).Should().BeFalse();
        missing.Should().BeNull();
    }

    [Fact]
    public void TryGetValue_string_key_uses_utf8_bytes()
    {
        var storages = BuildStorage(
            (Encoding.UTF8.GetBytes("alice"), [0x01]),
            (Encoding.UTF8.GetBytes("bob"), [0x02]));

        storages.TryGetValue("alice", out var item).Should().BeTrue();
        item!.Value.ToArray().Should().Equal(0x01);

        storages.TryGetValue("carol", out var missing).Should().BeFalse();
        missing.Should().BeNull();
    }

    [Fact]
    public void TryGetValue_UInt160_key_matches_raw_bytes()
    {
        var hash = UInt160.Parse("0x0102030405060708090a0b0c0d0e0f1011121314");
        var hashBytes = hash.ToArray();
        var storages = BuildStorage((hashBytes, new byte[] { 0xab }));

        storages.TryGetValue(hash, out var item).Should().BeTrue();
        item!.Value.ToArray().Should().Equal(0xab);

        var other = UInt160.Parse("0x1414141414141414141414141414141414141414");
        storages.TryGetValue(other, out var missing).Should().BeFalse();
        missing.Should().BeNull();
    }

    [Fact]
    public void TryGetValue_UInt256_key_matches_raw_bytes()
    {
        var hash = UInt256.Parse("0x0101010101010101010101010101010101010101010101010101010101010101");
        var hashBytes = hash.ToArray();
        var storages = BuildStorage((hashBytes, new byte[] { 0xcd }));

        storages.TryGetValue(hash, out var item).Should().BeTrue();
        item!.Value.ToArray().Should().Equal(0xcd);

        var other = UInt256.Parse("0x0202020202020202020202020202020202020202020202020202020202020202");
        storages.TryGetValue(other, out var missing).Should().BeFalse();
        missing.Should().BeNull();
    }

    private static IReadOnlyDictionary<ReadOnlyMemory<byte>, StorageItem> BuildStorage(params (byte[] key, byte[] value)[] entries)
    {
        var map = new Dictionary<ReadOnlyMemory<byte>, StorageItem>(MemorySequenceComparer.Default);
        foreach (var (key, value) in entries)
        {
            map[key] = new StorageItem(value);
        }
        return map;
    }

    private static byte[] Concat(byte[] left, byte[] right)
    {
        var combined = new byte[left.Length + right.Length];
        Buffer.BlockCopy(left, 0, combined, 0, left.Length);
        Buffer.BlockCopy(right, 0, combined, left.Length, right.Length);
        return combined;
    }
}
