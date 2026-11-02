// Copyright (C) 2015-2026 The Neo Project.
//
// ZipStorerTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.


using System.IO.Compression;
using System.IO;
using System.Text;
using System;
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

    [Fact]
    public void ReadCentralDir_ignores_a_short_trailing_record_in_the_central_directory()
    {
        // Regression test: the 46-byte minimum-length check used to run before
        // the signature check, so a valid archive with a short (<46 byte)
        // trailing record (e.g. a digital signature record 0x05054b50) inside
        // the declared central directory image was rejected with
        // InvalidDataException, though the scan previously (and should still)
        // stop gracefully at the unrecognized signature.
        var bytes = CreateZip(1, 0, 0);

        // digital signature record: 4-byte signature + 4 payload bytes (<46)
        var trailingRecord = new byte[] { 0x50, 0x4B, 0x05, 0x05, 0x01, 0x02, 0x03, 0x04 };

        // insert the trailing record after the last central directory entry,
        // before the end-of-central-directory record, and count it as part of
        // the declared central directory size
        var eocdOffset = bytes.Length - 22;
        var centralSize = BitConverter.ToUInt32(bytes, eocdOffset + 12);
        BitConverter.GetBytes(centralSize + (uint)trailingRecord.Length).CopyTo(bytes, eocdOffset + 12);

        var withTrailingRecord = new byte[bytes.Length + trailingRecord.Length];
        Array.Copy(bytes, 0, withTrailingRecord, 0, eocdOffset);
        Array.Copy(trailingRecord, 0, withTrailingRecord, eocdOffset, trailingRecord.Length);
        Array.Copy(bytes, eocdOffset, withTrailingRecord, eocdOffset + trailingRecord.Length, 22);

        using var zip = ZipStorer.Open(new MemoryStream(withTrailingRecord), FileAccess.Read);

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

    [Fact]
    public void ReadCentralDir_skips_truncated_ntfs_extra_record()
    {
        // Regression test: an NTFS extra field (header id 0x000A) whose tag/size
        // header promises 24 bytes of filetime data, but which is truncated
        // before the three 8-byte values fit in the buffer, used to throw
        // ArgumentOutOfRangeException from BitConverter.ToInt64. Truncated
        // records must be skipped so the entry keeps its DOS timestamp.
        var extra = new MemoryStream();
        var extraWriter = new BinaryWriter(extra);
        extraWriter.Write((ushort)0x000A);  // header id: NTFS
        extraWriter.Write((ushort)32);      // data length (more bytes than present)
        extraWriter.Write(0u);              // reserved
        extraWriter.Write((ushort)0x0001);  // tag 1: filetimes
        extraWriter.Write((ushort)24);      // tag size
        extraWriter.Write(ValidFileTime);   // mtime: only 8 of 24 bytes present

        var bytes = CreateZipWithCentralDirExtra(extra.ToArray());

        using var zip = ZipStorer.Open(new MemoryStream(bytes), FileAccess.Read);
        var entries = zip.ReadCentralDir();

        var entry = Assert.Single(entries);
        Assert.Equal("a", entry.FilenameInZip);
        // truncated filetimes are skipped, entry keeps the DOS timestamp
        Assert.Equal(new DateTime(1980, 1, 1), entry.ModifyTime);
    }

    [Fact]
    public void ReadCentralDir_skips_truncated_zip64_extra_record()
    {
        // Regression test: a ZIP64 extra field (header id 0x0001) whose tag/size
        // header promises 24 bytes of data, but which is truncated before the
        // first 8-byte value fits in the buffer, used to throw
        // ArgumentOutOfRangeException from BitConverter.ToInt64. Truncated
        // records must be skipped so the entry is still enumerated.
        var extra = new MemoryStream();
        var extraWriter = new BinaryWriter(extra);
        extraWriter.Write((ushort)0x0001);   // header id: ZIP64
        extraWriter.Write((ushort)32);       // data length (more bytes than present)
        extraWriter.Write(0u);               // reserved
        extraWriter.Write((ushort)0x0001);   // tag 1
        extraWriter.Write((ushort)24);       // tag size
        extraWriter.Write(0x11223344u);      // only 4 of 24 bytes present

        var bytes = CreateZipWithCentralDirExtra(
            extra.ToArray(), fileSize: 0xFFFFFFFF, compressedSize: 0xFFFFFFFF);

        using var zip = ZipStorer.Open(new MemoryStream(bytes), FileAccess.Read);
        var entries = zip.ReadCentralDir();

        var entry = Assert.Single(entries);
        Assert.Equal("a", entry.FilenameInZip);
    }

    static byte[] CreateZipWithCentralDirExtra(byte[] extraField, uint fileSize = 1u, uint compressedSize = 1u)
    {
        // Minimal stored (uncompressed) zip with a single one-byte entry "a"
        // whose central directory record carries the given (possibly
        // malformed) extra field bytes verbatim.
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
        writer.Write(compressedSize);       // compressed size
        writer.Write(fileSize);             // uncompressed size
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
        writer.Write(compressedSize);       // compressed size
        writer.Write(fileSize);             // uncompressed size
        writer.Write((ushort)1);            // filename length
        writer.Write((ushort)extraField.Length); // extra length
        writer.Write((ushort)0);            // comment length
        writer.Write((ushort)0);            // disk number start
        writer.Write((ushort)0);            // internal attributes
        writer.Write(0u);                   // external attributes
        writer.Write(0u);                   // header offset
        writer.Write((byte)'a');             // filename
        writer.Write(extraField);

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
