// Copyright (C) 2015-2026 The Neo Project.
//
// TraceDebugStreamTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BlockchainToolkit.TraceDebug;
using System;
using System.IO;
using Xunit;

namespace test.bctklib;

public class TraceDebugStreamTests
{
    [Fact]
    public void WriteFailureIsExposedAndStopsFurtherWrites()
    {
        using var stream = new WriteFailingStream();
        var sink = new TraceDebugStream(stream);

        sink.ProtocolSettings(894710606, 53);
        sink.ProtocolSettings(894710606, 53);

        Assert.Equal(1, stream.WriteCount);
        Assert.IsType<IOException>(sink.WriteError);

        sink.Dispose();

        Assert.IsType<IOException>(sink.WriteError);
    }

    private sealed class WriteFailingStream : MemoryStream
    {
        public int WriteCount { get; private set; }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            WriteCount++;
            throw new IOException("write failed");
        }
    }
}
