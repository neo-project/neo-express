// Copyright (C) 2015-2026 The Neo Project.
//
// RecordScriptTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Collector;
using Neo.Collector.Models;
using System;
using Xunit;

namespace test_collector;

public class RecordScriptTests
{
    [Fact]
    public void RecordScript_ignores_duplicate_script_for_same_contract()
    {
        // A contract present in the coverage output as both a .nef and a .neo-script
        // records its script twice. The second call must not throw, which would
        // otherwise drop the contract's coverage.
        var debugInfo = new NeoDebugInfo(
            Hash160.Zero,
            string.Empty,
            Array.Empty<string>(),
            Array.Empty<NeoDebugInfo.Method>());
        var collector = new ContractCoverageCollector("contract", debugInfo);
        var script = new byte[] { (byte)OpCode.PUSH1, (byte)OpCode.RET };

        collector.RecordScript(script.EnumerateInstructions());
        var exception = Record.Exception(() => collector.RecordScript(script.EnumerateInstructions()));

        Assert.Null(exception);
    }
}
