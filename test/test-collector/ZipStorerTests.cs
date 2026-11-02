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
using System.Text;
using Xunit;

namespace test_collector;

public class ZipStorerTests
{
    // Builds a minimal valid zip (local file header + central directory
    // record + end-of-central-directory) containing one stored entry "a",
    // with an arbitrary DOS date/time planted in the central directory
    // record. The DOS timestamp is optional metadata read from untrusted
    // input, so out-of-range bit fields must not crash ReadCentralDir.
    static byte[] CreateZipWithDosTime(ushort dosTime, ushort dosDate)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        // Local file header (30 bytes) + filename "a" + 1 byte of data
        bw.Write(0x04034b50u);  // local file header signature
        bw.Write((ushort)20);   // version needed
        bw.Write((ushort)0);    // general purpose flags
        bw.Write((ushort)0);    // method: store
        bw.Write((ushort)0);    // last mod time
        bw.Write((ushort)0x0021); // last mod date (1980-01-01)
        bw.Write(0u);           // crc32 (unused here)
        bw.Write(1u);           // compressed size
        bw.Write(1u);           // uncompressed size
        bw.Write((ushort)1);    // filename length
        bw.Write((ushort)0);    // extra field length
        bw.Write(Encoding.UTF8.GetBytes("a"));
        bw.Write((byte)'a');    // stored data

        // Central directory record (46 bytes) + filename "a"
        bw.Write(0x02014b50u);  // central file header signature
        bw.Write((ushort)0x030B); // version made by
        bw.Write((ushort)20);   // version needed
        bw.Write((ushort)0);    // general purpose flags
        bw.Write((ushort)0);    // method: store
        bw.Write(dosTime);      // last mod file time
        bw.Write(dosDate);      // last mod file date
        bw.Write(0u);           // crc32 (unused here)
        bw.Write(1u);           // compressed size
        bw.Write(1u);           // uncompressed size
        bw.Write((ushort)1);    // filename length
        bw.Write((ushort)0);    // extra field length
        bw.Write((ushort)0);    // file comment length
        bw.Write((ushort)0);    // disk number start
        bw.Write((ushort)0);    // internal file attributes
        bw.Write(0u);           // external file attributes
        bw.Write(0u);           // offset of local header
        bw.Write(Encoding.UTF8.GetBytes("a"));

        // End of central directory record (22 bytes)
        bw.Write(0x06054b50u);  // end of central dir signature
        bw.Write((ushort)0);    // this disk
        bw.Write((ushort)0);    // disk with start of central dir
        bw.Write((ushort)1);    // entries on this disk
        bw.Write((ushort)1);    // total entries
        bw.Write((uint)47);     // size of central directory
        bw.Write((uint)32);     // offset of central directory
        bw.Write((ushort)0);    // zip file comment length

        bw.Flush();
        return ms.ToArray();
    }

    [Theory]
    [InlineData((ushort)0xC000)] // hour = 24
    [InlineData((ushort)0x0780)] // minute = 60
    [InlineData((ushort)0x001E)] // second = 60 (second/2 = 30)
    public void ReadCentralDir_falls_back_when_dos_time_is_out_of_range(ushort dosTime)
    {
        var bytes = CreateZipWithDosTime(dosTime, dosDate: 0x0021);

        using var stream = new MemoryStream(bytes);
        using var zip = ZipStorer.Open(stream, FileAccess.Read);
        var entries = zip.ReadCentralDir();

        var entry = Assert.Single(entries);
        Assert.Equal("a", entry.FilenameInZip);
        Assert.True(entry.ModifyTime > DateTime.MinValue);
    }

    [Fact]
    public void ReadCentralDir_falls_back_when_dos_day_is_out_of_range_for_month()
    {
        // 1980-02-30: valid month/day bit ranges, but Feb has no day 30.
        // Packed DOS date: day 30 | month 2 << 5 | (year - 1980) << 9 = 0x005E.
        var bytes = CreateZipWithDosTime(dosTime: 0x0000, dosDate: 0x005E);

        using var stream = new MemoryStream(bytes);
        using var zip = ZipStorer.Open(stream, FileAccess.Read);
        var entries = zip.ReadCentralDir();

        var entry = Assert.Single(entries);
        Assert.Equal("a", entry.FilenameInZip);
        Assert.True(entry.ModifyTime > DateTime.MinValue);
    }

    [Fact]
    public void ReadCentralDir_accepts_a_valid_dos_time()
    {
        // 1980-01-01 12:34:56
        var bytes = CreateZipWithDosTime(
            dosTime: (ushort)((56 / 2) | (34 << 5) | (12 << 11)),
            dosDate: 0x0021);

        using var stream = new MemoryStream(bytes);
        using var zip = ZipStorer.Open(stream, FileAccess.Read);
        var entries = zip.ReadCentralDir();

        var entry = Assert.Single(entries);
        Assert.Equal("a", entry.FilenameInZip);
        Assert.Equal(new DateTime(1980, 1, 1, 12, 34, 56), entry.ModifyTime);
    }
}
