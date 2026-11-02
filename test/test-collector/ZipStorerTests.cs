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
    // Regression tests for a fuzzing-discovered crash (artifacts/fuzz/crashes/zip-1.bin):
    // ReadCentralDir used the untrusted filename/extra/comment lengths declared in a
    // central directory record to slice the central directory image without checking
    // that they fit, throwing ArgumentOutOfRangeException ("Index and count must refer
    // to a location within the buffer.") instead of rejecting the malformed archive.
    // The archives below are minimized equivalents of the crash input: a single stored
    // entry whose central directory record declares a length that overruns the image.

    [Theory]
    [InlineData(0xFFFF, 0, 0)]   // declared filename length overruns the central directory
    [InlineData(1, 0, 0xFFFF)]   // declared comment length overruns the central directory
    [InlineData(1, 0xFFFF, 0)]   // declared extra field length overruns the central directory
    public void ReadCentralDir_rejects_entries_whose_declared_lengths_overrun_the_central_directory(
        ushort filenameSize, ushort extraSize, ushort commentSize)
    {
        var bytes = CreateZip(filenameSize, extraSize, commentSize);

        using var zip = ZipStorer.Open(new MemoryStream(bytes), FileAccess.Read);

        var exception = Assert.Throws<InvalidDataException>(() => zip.ReadCentralDir());
        Assert.Contains("central directory", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadCentralDir_rejects_a_truncated_central_directory_entry()
    {
        // The end-of-central-directory record claims a central directory size of only
        // 10 bytes, less than the 46-byte fixed portion of a central directory entry.
        var bytes = CreateZip(1, 0, 0);
        bytes[90] = 10; // patch centralSize in the end-of-central-directory record

        using var zip = ZipStorer.Open(new MemoryStream(bytes), FileAccess.Read);

        var exception = Assert.Throws<InvalidDataException>(() => zip.ReadCentralDir());
        Assert.Contains("central directory", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadCentralDir_reads_a_valid_entry()
    {
        var bytes = CreateZip(1, 0, 0);

        using var zip = ZipStorer.Open(new MemoryStream(bytes), FileAccess.Read);

        var entries = zip.ReadCentralDir();

        var entry = Assert.Single(entries);
        Assert.Equal("a", entry.FilenameInZip);
    }

    // Builds a minimal zip with one stored entry "a". The central directory record
    // declares the given filename, extra field and comment lengths while the image
    // actually contains only the single byte filename, mimicking a mutated archive.
    static byte[] CreateZip(ushort filenameSize, ushort extraSize, ushort commentSize)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // local file header for entry "a" (store; the reader never needs the data)
        writer.Write(0x04034b50u);     // local file header signature
        writer.Write((ushort)20);      // version needed to extract
        writer.Write((ushort)0);       // general purpose bit flag
        writer.Write((ushort)0);       // compression method: store
        writer.Write((ushort)0);       // last mod file time
        writer.Write((ushort)0x21);    // last mod file date (1980-01-01)
        writer.Write(0u);              // crc-32
        writer.Write(0u);              // compressed size
        writer.Write(0u);              // uncompressed size
        writer.Write((ushort)1);       // filename length
        writer.Write((ushort)0);       // extra field length
        writer.Write(Encoding.ASCII.GetBytes("a"));

        var centralDirOffset = (uint)ms.Length;

        // central directory file header
        writer.Write(0x02014b50u);     // central file header signature
        writer.Write((ushort)20);      // version made by
        writer.Write((ushort)20);      // version needed to extract
        writer.Write((ushort)0);       // general purpose bit flag
        writer.Write((ushort)0);       // compression method: store
        writer.Write((ushort)0);       // last mod file time
        writer.Write((ushort)0x21);    // last mod file date (1980-01-01)
        writer.Write(0u);              // crc-32
        writer.Write(0u);              // compressed size
        writer.Write(0u);              // uncompressed size
        writer.Write(filenameSize);    // filename length (declared, untrusted)
        writer.Write(extraSize);       // extra field length (declared, untrusted)
        writer.Write(commentSize);     // file comment length (declared, untrusted)
        writer.Write((ushort)0);       // disk number start
        writer.Write((ushort)0);       // internal file attributes
        writer.Write(0u);              // external file attributes
        writer.Write(centralDirOffset);// relative offset of local header
        writer.Write(Encoding.ASCII.GetBytes("a")); // only one filename byte is present

        var centralDirSize = (uint)(ms.Length - centralDirOffset);

        // end of central directory record
        writer.Write(0x06054b50u);     // end of central dir signature
        writer.Write((ushort)0);       // number of this disk
        writer.Write((ushort)0);       // disk with start of central directory
        writer.Write((ushort)1);       // entries on this disk
        writer.Write((ushort)1);       // total entries
        writer.Write(centralDirSize);  // size of central directory
        writer.Write(centralDirOffset);// offset of start of central directory
        writer.Write((ushort)0);       // zipfile comment length

        return ms.ToArray();
    }
}
