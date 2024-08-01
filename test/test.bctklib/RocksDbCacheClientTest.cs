// Copyright (C) 2015-2024 The Neo Project.
//
// RocksDbCacheClientTest.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

// namespace test.bctklib;

// using static Utility;

// public class RocksDbCacheClientTest
// {
//     WriteOptions syncWriteOptions = new WriteOptions().SetSync(true);

//     [Fact]
//     public void cached_get_state_returns_expected()
//     {
//         using var store = new Neo.Persistence.MemoryStore();
//         var trie = GetTestTrie(store);
//         var key = BitConverter.GetBytes(42);
//         var proof = trie.GetSerializedProof(key);
//         Assert.True(trie.TryGetValue(key, out var expected));

//         using var rpcClient = new TestableRpcClient(() => new JString(Convert.ToBase64String(trie.GetSerializedProof(key))));

//         var tempPath = new CleanupPath();
//         using var client = new StateServiceStore.RocksDbCacheClient(rpcClient, tempPath);

//         Assert.Null(client.GetCachedState(trie.Root.Hash, UInt160.Zero, key));
//         var actual1 = client.GetState(trie.Root.Hash, UInt160.Zero, key);
//         Assert.Equal(expected, actual1);
//         Assert.NotNull(client.GetCachedState(trie.Root.Hash, UInt160.Zero, key));

//         var actual2 = client.GetState(trie.Root.Hash, UInt160.Zero, key);
//         Assert.Equal(expected, actual2);
//     }

//     [Theory]
//     [InlineData(-2146232969, "The given key was not present in the dictionary.")]
//     [InlineData(-2146232969, "Halt and catch fire.")]
//     [InlineData(-100, "Unknown value")]
//     public void cached_get_state_returns_null_for_key_not_found_exception(int code, string msg)
//     {
//         var key = Neo.Utility.StrictUTF8.GetBytes("key");

//         using var rpcClient = new TestableRpcClient(() => throw new RpcException(code, msg));

//         var tempPath = new CleanupPath();
//         using var client = new StateServiceStore.RocksDbCacheClient(rpcClient, tempPath);

//         Assert.Null(client.GetCachedState(UInt256.Zero, UInt160.Zero, key));
//         var actual1 = client.GetState(UInt256.Zero, UInt160.Zero, key);
//         Assert.Null(actual1);
//         Assert.NotNull(client.GetCachedState(UInt256.Zero, UInt160.Zero, key));

//         var actual2 = client.GetState(UInt256.Zero, UInt160.Zero, key);
//         Assert.Null(actual2);
//     }

//     // [Fact]
//     // public void cached_get_state_returns_null_for_key_not_found_exception_workaround()
//     // {
//     //     var key = Neo.Utility.StrictUTF8.GetBytes("key");

//     //     using var rpcClient = new TestableRpcClient(() => throw new RpcException(-2146232969, "The given key was not present in the dictionary."));

//     //     var tempPath = RocksDbUtility.GetTempPath();
//     //     using var _ = Utility.GetDeleteDirectoryDisposable(tempPath);
//     //     using var client = new StateServiceStore.RocksDbCacheClient(rpcClient, tempPath);

//     //     var actual1 = client.GetState(UInt256.Zero, UInt160.Zero, key);
//     //     var actual2 = client.GetState(UInt256.Zero, UInt160.Zero, key);
//     //     Assert.Null(actual1);
//     //     Assert.Null(actual2);
//     // }
// }
