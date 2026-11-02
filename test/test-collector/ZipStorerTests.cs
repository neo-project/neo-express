// Copyright (C) 2015-2026 The Neo Project.
//
// ZipStorerTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Collector.Models;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace test_collector;

public class ZipStorerTests
{
    // Verbatim bytes of the fuzzing crash input saved at
    // artifacts/fuzz/crashes/zip-3.bin: ZipStorer.GetFileOffset seeked the
    // underlying stream to an offset computed from untrusted zip fields
    // without validating it against the stream length. The offset arithmetic
    // overflowed, so mutated values either crashed with
    // System.ArgumentOutOfRangeException (zip-3: negative seek offset) or
    // produced silently wrong FileOffset values. Malformed archives must be
    // rejected with InvalidDataException instead.
    const string ZIP3_CRASH_INPUT_HEX
        = "504B0304140000000800C0632F58A1A5BB2A6700000071000000100048006F746865722E64656275672E6A736F6E0100200000000000010018000000000000000000000000000000000000000000000000000A0020000000000001001800001446806B47DA01001446806B47DA01001446806B47DA014CCA410A84300C46E1BBFC6B156B8BC45E657091A6966E3460230CC8DC7D70E7F67DEF46E55611317E49265FDC326762974AA6E038B04F4B988853A032BAE49F051DB2CAB56F87F5A7AA21E2951AE207A2879D2C3648C3DA61DFAC6A7E64FDFD010000FFFF0300504B0102170B140000000800C0632F58A1A5BB2A6700000071000000100048000000000000000000008100FFFFFFFFFFFFFFFFFF64656275672E6A736F6E0100200000000000010018007100000000000000670000000000000000000000000000000A0020000000000001001800001446806B47DA01001446806B47DA01001446806B47DA01504B06062C000000000000002D002D000000000000000000010000000000000001000000000000008600000000000000DD00000000000000504B060700000000630100000000000001000000504B050600000000FFFFFFFFFFFFFFFFFFFFFFFF0000";

    // Minimal 104-byte zip archive with a single "a.json" entry. The local
    // header is followed directly by the central directory record and the
    // end-of-central-directory record. Both the central directory's local
    // header offset field and the local header's filename length field are
    // attacker controlled.
    static byte[] CreateZipBytes(uint localHeaderOffset, ushort localFilenameLength)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // local file header (30 bytes, no filename/extra/data)
        writer.Write(0x04034b50u);         // local file header signature
        writer.Write((ushort)20);          // version needed to extract
        writer.Write((ushort)0);           // general purpose bit flag
        writer.Write((ushort)0);           // compression method (store)
        writer.Write((ushort)0);           // last mod file time
        writer.Write((ushort)0);           // last mod file date
        writer.Write(0u);                  // crc-32
        writer.Write(0u);                  // compressed size
        writer.Write(0u);                  // uncompressed size
        writer.Write(localFilenameLength); // filename length
        writer.Write((ushort)0);           // extra field length

        // central directory record (46 bytes + 6-byte filename "a.json")
        writer.Write(0x02014b50u);         // central directory signature
        writer.Write((ushort)20);          // version made by
        writer.Write((ushort)20);          // version needed to extract
        writer.Write((ushort)0);           // general purpose bit flag
        writer.Write((ushort)0);           // compression method (store)
        writer.Write((ushort)0);           // last mod file time
        writer.Write((ushort)0);           // last mod file date
        writer.Write(0u);                  // crc-32
        writer.Write(0u);                  // compressed size
        writer.Write(0u);                  // uncompressed size
        writer.Write((ushort)6);           // filename length
        writer.Write((ushort)0);           // extra field length
        writer.Write((ushort)0);           // file comment length
        writer.Write((ushort)0);           // disk number start
        writer.Write((ushort)0);           // internal file attributes
        writer.Write(0u);                  // external file attributes
        writer.Write(localHeaderOffset);   // relative offset of local header
        writer.Write(Encoding.UTF8.GetBytes("a.json"));

        // end of central directory record (22 bytes)
        writer.Write(0x06054b50u);         // end of central directory signature
        writer.Write((ushort)0);           // number of this disk
        writer.Write((ushort)0);           // disk with start of central directory
        writer.Write((ushort)1);           // entries on this disk
        writer.Write((ushort)1);           // total entries
        writer.Write(52u);                 // size of central directory
        writer.Write(30u);                 // offset of central directory
        writer.Write((ushort)0);           // comment length

        return stream.ToArray();
    }

    static void AssertReadCentralDirRejected(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var zip = ZipStorer.Open(stream, FileAccess.Read);

        var ex = Assert.Throws<InvalidDataException>(() => zip.ReadCentralDir());
        Assert.False(string.IsNullOrEmpty(ex.Message));
    }

    [Fact]
    public void fuzz_crash_input_zip3_is_rejected_with_InvalidDataException()
    {
        AssertReadCentralDirRejected(Convert.FromHexString(ZIP3_CRASH_INPUT_HEX));
    }

    [Fact]
    public void zip_local_header_offset_out_of_range_throws_InvalidDataException()
    {
        // Central directory entry points at local header offset 0xFFFFFFFF,
        // far beyond the end of the 104-byte archive
        AssertReadCentralDirRejected(CreateZipBytes(uint.MaxValue, 0));
    }

    [Fact]
    public void zip_file_offset_out_of_range_throws_InvalidDataException()
    {
        // Local header filename length (0xFF00) produces a file offset far
        // beyond the end of the 104-byte archive
        AssertReadCentralDirRejected(CreateZipBytes(0, 0xFF00));
    }

    [Fact]
    public void zip_local_header_offset_out_of_range_is_rejected_by_TryLoadCompressed()
    {
        var bytes = CreateZipBytes(uint.MaxValue, 0);

        using var stream = new MemoryStream(bytes);
        Assert.False(NeoDebugInfo.TryLoadCompressed(stream, out var debugInfo));
        Assert.Null(debugInfo);
    }
}
