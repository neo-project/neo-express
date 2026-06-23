// Copyright (C) 2015-2026 The Neo Project.
//
// PersistStoragePairTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.Persistence;
using Neo.Persistence.Providers;
using NeoExpress.Node;
using System;
using Xunit;

namespace test.workflowvalidation;

public class PersistStoragePairTests
{
    const int ContractId = 1;

    // A malformed base64 key/value is a user input error and must surface, not be reported
    // as the benign "already exists" result.
    [Fact]
    public void PersistStoragePair_throws_on_malformed_base64()
    {
        using var store = new MemoryStore();
        using var snapshot = new StoreCache(store.GetSnapshot());

        var act = () => NodeUtility.PersistStoragePair(snapshot, ContractId, ("###not-base64###", "AQI="));

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void PersistStoragePair_returns_false_when_the_key_already_exists()
    {
        using var store = new MemoryStore();
        using var snapshot = new StoreCache(store.GetSnapshot());

        NodeUtility.PersistStoragePair(snapshot, ContractId, ("AQ==", "Ag==")).Should().BeTrue();
        NodeUtility.PersistStoragePair(snapshot, ContractId, ("AQ==", "Aw==")).Should().BeFalse();
    }
}
