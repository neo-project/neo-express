// Copyright (C) 2015-2026 The Neo Project.
//
// OfflineNodeTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.Network.P2P.Payloads;
using NeoExpress.Node;
using System;
using Xunit;

namespace test.workflowvalidation;

public class OfflineNodeTests
{
    [Fact]
    public void FormatScriptContainer_returns_empty_for_a_null_container()
    {
        // A log emitted without a transaction container (e.g. a verification
        // trigger) must not dereference the null container.
        OfflineNode.FormatScriptContainer(null).Should().Be(string.Empty);
    }

    [Fact]
    public void FormatScriptContainer_wraps_the_type_name_when_present()
    {
        var tx = new Transaction
        {
            Signers = Array.Empty<Signer>(),
            Attributes = Array.Empty<TransactionAttribute>(),
            Witnesses = Array.Empty<Witness>(),
        };
        OfflineNode.FormatScriptContainer(tx).Should().Be(" [Transaction]");
    }
}
