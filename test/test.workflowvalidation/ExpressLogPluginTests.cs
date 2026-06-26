// Copyright (C) 2015-2026 The Neo Project.
//
// ExpressLogPluginTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.Network.P2P.Payloads;
using Neo.VM;
using NeoExpress.Node;
using System;
using Xunit;

namespace test.workflowvalidation;

public class ExpressLogPluginTests
{
    [Fact]
    public void FormatFaultLog_labels_a_block_level_fault_with_a_null_transaction()
    {
        // Block-level OnPersist/PostPersist executions have a null Transaction;
        // formatting a fault must not dereference it.
        var message = ExpressLogPlugin.FormatFaultLog(VMState.FAULT, null, new Exception("boom"));

        message.Should().Be("Tx FAULT: hash=(block) exception=\"boom\"");
    }

    [Fact]
    public void FormatFaultLog_uses_the_transaction_hash_when_present()
    {
        var tx = new Transaction
        {
            Signers = Array.Empty<Signer>(),
            Attributes = Array.Empty<TransactionAttribute>(),
            Witnesses = Array.Empty<Witness>(),
            Script = Array.Empty<byte>(),
        };

        var message = ExpressLogPlugin.FormatFaultLog(VMState.FAULT, tx, null);

        message.Should().Be($"Tx FAULT: hash={tx.Hash}");
    }

    [Fact]
    public void FormatFaultLog_returns_null_for_a_non_fault_state()
    {
        ExpressLogPlugin.FormatFaultLog(VMState.HALT, null, null).Should().BeNull();
    }
}
