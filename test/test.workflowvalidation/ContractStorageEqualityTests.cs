// Copyright (C) 2015-2026 The Neo Project.
//
// ContractStorageEqualityTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.Persistence;
using Neo.Persistence.Providers;
using Neo.SmartContract;
using NeoExpress.Node;
using Xunit;

namespace test.workflowvalidation;

public class ContractStorageEqualityTests
{
    const int ContractId = 1;

    [Fact]
    public void ContractStorageEquals_returns_true_for_identical_storage()
    {
        var storagePairs = new[]
        {
            CreateStoragePair([0x01], [0x02]),
            CreateStoragePair([0x03], [0x04]),
        };

        using var store = new MemoryStore();
        using var snapshot = new StoreCache(store.GetSnapshot());
        PersistStoragePairs(snapshot, ContractId, storagePairs);

        NodeUtility.ContractStorageEquals(ContractId, snapshot, storagePairs)
            .Should().BeTrue();
    }

    [Fact]
    public void ContractStorageEquals_returns_false_when_downloaded_storage_has_extra_entries()
    {
        var localStoragePairs = new[]
        {
            CreateStoragePair([0x01], [0x02]),
        };
        var downloadedStoragePairs = new[]
        {
            CreateStoragePair([0x01], [0x02]),
            CreateStoragePair([0x03], [0x04]),
        };

        using var store = new MemoryStore();
        using var snapshot = new StoreCache(store.GetSnapshot());
        PersistStoragePairs(snapshot, ContractId, localStoragePairs);

        NodeUtility.ContractStorageEquals(ContractId, snapshot, downloadedStoragePairs)
            .Should().BeFalse();
    }

    static (string key, string value) CreateStoragePair(byte[] key, byte[] value)
    {
        return (Convert.ToBase64String(key), Convert.ToBase64String(value));
    }

    static void PersistStoragePairs(DataCache snapshot, int contractId, IReadOnlyList<(string key, string value)> storagePairs)
    {
        foreach (var (key, value) in storagePairs)
        {
            snapshot.Add(
                new StorageKey { Id = contractId, Key = Convert.FromBase64String(key) },
                new StorageItem(Convert.FromBase64String(value)));
        }
    }
}
