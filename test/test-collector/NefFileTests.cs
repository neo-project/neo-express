// Copyright (C) 2015-2026 The Neo Project.
//
// NefFileTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Collector.Models;
using System;
using System.IO;
using Xunit;

namespace test_collector;

public class NefFileTests
{
    const string NefResourceName = "0xe8c2cf8b50016c94f6eafbdd024febc6cd0672fe.nef";

    static byte[] GetNefBytes()
    {
        using var stream = TestFiles.GetResourceStream(NefResourceName);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    [Fact]
    public void Load_accepts_a_real_nef_with_a_valid_checksum()
    {
        // A real compiled contract round-trips, which also confirms the checksum
        // is computed the same way the compiler writes it.
        var nef = NefFile.Load(new MemoryStream(GetNefBytes()));

        Assert.NotEmpty(nef.Script);
    }

    [Fact]
    public void Load_rejects_a_nef_whose_checksum_does_not_match()
    {
        var bytes = GetNefBytes();
        bytes[^1] ^= 0xFF; // corrupt the trailing checksum

        Assert.Throws<FormatException>(() => NefFile.Load(new MemoryStream(bytes)));
    }
}
