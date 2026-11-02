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
    const long ValidFileTime = 0x01D0_0000_0000_0000;

    [Fact]
    public void ReadCentralDir_ignores_out_of_range_ntfs_filetime()
    {
        // Regression test: an NTFS extra field whose creation filetime is out
        // of the range accepted by DateTime.FromFileTime used to throw
        // ArgumentOutOfRangeException from ZipStorer.ReadExtraInfo.
        // Malformed extra-field timestamps must be skipped so the entry falls
        // back to the DOS timestamp from the central directory record.
        var bytes = CreateZipWithNtfsExtraTimes(
            modifyFileTime: ValidFileTime,
            accessFileTime: ValidFileTime,
            creationFileTime: -1);

        using var zip = ZipStorer.Open(new MemoryStream(bytes), FileAccess.Read);
        var entries = zip.ReadCentralDir();

        var entry = Assert.Single(entries);
        Assert.Equal("a", entry.FilenameInZip);
        Assert.Equal(DateTime.FromFileTime(ValidFileTime), entry.ModifyTime);
        // out-of-range creation filetime falls back to the DOS timestamp
        Assert.Equal(new DateTime(1980, 1, 1), entry.CreationTime);
    }

    static byte[] CreateZipWithNtfsExtraTimes(long modifyFileTime, long accessFileTime, long creationFileTime)
    {
        // Minimal stored (uncompressed) zip with a single one-byte entry "a"
        // whose central directory record carries an NTFS extra field
        // (header id 0x000A, tag 0x0001) with the given filetimes.
        var buffer = new MemoryStream();
        var writer = new BinaryWriter(buffer, Encoding.UTF8);

        // local file header
        writer.Write(0x04034b50u);          // signature
        writer.Write((ushort)0x0014);       // version needed
        writer.Write((ushort)0x0000);       // flags
        writer.Write((ushort)0x0000);       // method: stored
        writer.Write((ushort)0x0000);       // dos time
        writer.Write((ushort)0x0021);       // dos date (1980-01-01)
        writer.Write(0u);                   // crc32
        writer.Write(1u);                   // compressed size
        writer.Write(1u);                   // uncompressed size
        writer.Write((ushort)1);            // filename length
        writer.Write((ushort)0);            // extra length
        writer.Write((byte)'a');             // filename
        writer.Write((byte)'x');             // file data

        var centralDirOffset = (uint)buffer.Length;

        // central directory record
        writer.Write(0x02014b50u);          // signature
        writer.Write((ushort)0x0014);       // version made by
        writer.Write((ushort)0x0014);       // version needed
        writer.Write((ushort)0x0000);       // flags
        writer.Write((ushort)0x0000);       // method: stored
        writer.Write((ushort)0x0000);       // dos time
        writer.Write((ushort)0x0021);       // dos date (1980-01-01)
        writer.Write(0u);                   // crc32
        writer.Write(1u);                   // compressed size
        writer.Write(1u);                   // uncompressed size
        writer.Write((ushort)1);            // filename length
        writer.Write((ushort)36);           // extra length
        writer.Write((ushort)0);            // comment length
        writer.Write((ushort)0);            // disk number start
        writer.Write((ushort)0);            // internal attributes
        writer.Write(0u);                   // external attributes
        writer.Write(0u);                   // header offset
        writer.Write((byte)'a');             // filename
        // NTFS extra field
        writer.Write((ushort)0x000A);       // header id: NTFS
        writer.Write((ushort)32);           // data length
        writer.Write(0u);                   // reserved
        writer.Write((ushort)0x0001);       // tag 1: filetimes
        writer.Write((ushort)24);           // tag size
        writer.Write(modifyFileTime);       // mtime
        writer.Write(accessFileTime);       // atime
        writer.Write(creationFileTime);     // ctime

        var centralDirSize = (uint)buffer.Length - centralDirOffset;

        // end of central directory record
        writer.Write(0x06054b50u);          // signature
        writer.Write((ushort)0);            // disk number
        writer.Write((ushort)0);            // central dir start disk
        writer.Write((ushort)1);            // entries on this disk
        writer.Write((ushort)1);            // total entries
        writer.Write(centralDirSize);       // central dir size
        writer.Write(centralDirOffset);     // central dir offset
        writer.Write((ushort)0);            // comment length

        return buffer.ToArray();
    }
}
