// Copyright (C) 2015-2024 The Neo Project.
//
// Utility.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BlockchainToolkit.Persistence;
using Neo.Cryptography.MPTTrie;
using Neo.IO;
using Neo.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace test.bctklib;

static class Utility
{
    public static byte[] Bytes(int value) => BitConverter.GetBytes(value);
    public static byte[] Bytes(string value) => System.Text.Encoding.UTF8.GetBytes(value);

    public struct CleanupPath : IDisposable
    {
        public readonly string Path;

        public CleanupPath()
        {
            Path = RocksDbUtility.GetTempPath();
        }

        public static implicit operator string(CleanupPath @this) => @this.Path;

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, true);
        }
    }

    public static IStore CreateNeoRocksDb(string path)
    {
        const string storeTypeName = "Neo.Plugins.Storage.Store";
        var storeType = typeof(Neo.Plugins.Storage.RocksDBStore).Assembly.GetType(storeTypeName);
        var storeCtor = storeType?.GetConstructor(new[] { typeof(string) });
        var store = storeCtor?.Invoke(new object[] { path }) as IStore;
        if (store == null)
            throw new Exception($"Failed to create {storeTypeName} instance");
        return store;
    }

    public static Stream GetResourceStream(string name)
    {
        var assembly = typeof(DebugInfoTest).Assembly;
        var resourceName = assembly.GetManifestResourceNames().SingleOrDefault(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException();
        return assembly.GetManifestResourceStream(resourceName) ?? throw new FileNotFoundException();
    }

    public static JToken GetResourceJson(string name)
    {
        using var resource = GetResourceStream(name);
        using var streamReader = new System.IO.StreamReader(resource);
        using var jsonReader = new JsonTextReader(streamReader);
        return JToken.ReadFrom(jsonReader);
    }

    public static string GetResource(string name)
    {
        using var resource = GetResourceStream(name);
        using var streamReader = new System.IO.StreamReader(resource);
        return streamReader.ReadToEnd();
    }

    static readonly IReadOnlyList<string> GreekLetters = new[]
    {
        "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta", "Iota", "Kappa",
        "Lambda", "Mu", "Nu", "Xi", "Omicron", "Pi", "Rho", "Sigma", "Tau", "Upsilon",
        "Phi", "Chi", "Psi", "Omega"
    };

    static byte[] IndexToKey(int i)
    {
        Debug.Assert(i > 0 && i < 100);
        var tens = i / 10;
        var ones = i % 10;
        return new byte[] { (byte)tens, (byte)ones };
    }

    public static IEnumerable<(byte[] key, byte[] value)> TestData =>
        GreekLetters.Select((s, i) => (IndexToKey(i + 1), Bytes(s)));


    public static void PutSeekData(this IStore store, (byte start, byte end) one, (byte start, byte end) two)
    {
        foreach (var (key, value) in GetSeekData(one, two))
        {
            store.Put(key, value);
        }
    }

    public static IEnumerable<(byte[], byte[])> GetSeekData((byte start, byte end) one, (byte start, byte end) two)
    {
        if (one.start > 9 || one.end > 9 || one.end < one.start)
            throw new ArgumentException("Invalid value", nameof(one));
        if (two.start > 9 || two.end > 9 || two.end < two.start)
            throw new ArgumentException("Invalid value", nameof(two));

        for (var i = one.start; i <= one.end; i++)
        {
            for (var j = two.start; j <= two.end; j++)
            {
                yield return (new[] { i, j }, BitConverter.GetBytes(i * 10 + j));
            }
        }
    }

    public static Trie GetTestTrie(Neo.Persistence.IStore store, uint count = 100)
    {
        using var snapshot = store.GetSnapshot();
        var trie = new Trie(snapshot, null);
        for (var i = 0; i < count; i++)
        {
            var key = BitConverter.GetBytes(i);
            var value = Neo.Utility.StrictUTF8.GetBytes($"{i}");
            trie.Put(key, value);
        }
        trie.Commit();
        snapshot.Commit();
        return trie;
    }

    public static byte[] GetSerializedProof(this Trie trie, byte[] key)
    {
        if (trie.TryGetProof(key, out var proof))
        {
            return SerializeProof(key, proof);
        }
        else
        {
            throw new KeyNotFoundException();
        }
    }

    public static byte[] SerializeProof(byte[] key, HashSet<byte[]> proof)
    {
        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms, Neo.Utility.StrictUTF8);

        writer.WriteVarBytes(key);
        writer.WriteVarInt(proof.Count);
        foreach (var item in proof)
        {
            writer.WriteVarBytes(item);
        }
        writer.Flush();
        return ms.ToArray();
    }
}
