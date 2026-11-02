// Copyright (C) 2015-2026 The Neo Project.
//
// ZipStorerTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Xunit;

namespace test_collector;

public class ZipStorerTests
{
    // Inline copy of the fuzzing crash input artifacts/fuzz/crashes/zip-4.bin
    // (453 bytes, transcribed from the input-hex record in zip-4.txt): a
    // single-entry zip whose central-directory record declares headerOffset
    // 0xFFFFFFFF. The untrusted offset must be rejected with a clean
    // InvalidDataException instead of reaching Stream.Seek and throwing
    // ArgumentOutOfRangeException.
    const string InvalidHeaderOffsetZipHex =
        "504B0304140000000800C0632F58A1A5BB2A6700000071000000100048006F746865722E6465" +
        "6275672E6A736F6E010020000000000001001800000000000000000000000000000000000000" +
        "0000000000000A0020000000000001001800001446806B47DA01001446806B47DA0100144680" +
        "6B47DA014CCA410A84300C46E1BBFC6B156B8BC45E657091A6966E3460230CC8DC7D70E7F67D" +
        "EF46E55611317E49265FDC326762974AA6E038B04F4B988853A032BAE49F051DB2CAB56F87F5" +
        "A7AA21E2951AE207A2879D2C3648C3DA61DFAC6A7E64FDFD010000FFFF0300504B0102170B14" +
        "0000000800C0632F58A1A5BB2A670000007100000010004800000000000000000000DDFFFFFF" +
        "FF6F746865722E64656275672E6A736F6E010020000000000001C21800710000000000000067" +
        "0000000000000000000000000000000A0020000000000001001800001446806B47DA01001446" +
        "806B47DA01001446806B47DA01504B06062C000000000000002D002D00000000000000000001" +
        "0000000000000001000000000000008600000000000000DD00000000000000504B0607000000" +
        "00630100000000000001000000504B050600000000FFFFFFFFFFFFFFFFFFFFFFFF0000";

    [Fact]
    public async Task ExtractFile_rejects_entry_with_out_of_range_header_offset()
    {
        var bytes = Convert.FromHexString(InvalidHeaderOffsetZipHex);

        // open/extract the same way the coverage collector loads .nefdbgnfo
        // archives (NeoDebugInfo.TryLoadCompressed)
        using var zip = ZipStorer.Open(new MemoryStream(bytes), FileAccess.Read);
        var entry = Assert.Single(zip.ReadCentralDir());

        var ex = await Assert.ThrowsAsync<System.IO.InvalidDataException>(
            () => zip.ExtractFileAsync(entry, new MemoryStream()));

        Assert.Contains("header offset", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
