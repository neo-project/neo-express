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
using System.Threading.Tasks;
using Xunit;

namespace test_collector;

public class ZipStorerTests
{
    // A hand-minimized 105-byte zip archive found by fuzzing ZipStorer.
    // It contains a single Store-method entry ("a") whose central directory
    // declares FileSize=1000 while only 74 bytes of payload exist after the
    // data offset (31). Extracting it must fail cleanly, not spin forever.
    // Ground truth: artifacts/fuzz/crashes/minimal/zip-store-hang.bin
    const string TruncatedStoreEntryZipHex =
        "504B0304140000000000000021000000" +
        "000005000000E8030000010000006168" +
        "656C6C6F504B01021400140000000000" +
        "000021000000000005000000E8030000" +
        "01000000000000000000000000000000" +
        "000061504B050600000000010001002F" +
        "000000240000000000";

    [Fact]
    public async Task ExtractFileAsync_rejects_truncated_stored_entry_instead_of_hanging()
    {
        var archiveBytes = Convert.FromHexString(TruncatedStoreEntryZipHex);
        Assert.Equal(105, archiveBytes.Length);

        using var zip = ZipStorer.Open(new MemoryStream(archiveBytes), FileAccess.Read);
        var entry = Assert.Single(zip.ReadCentralDir());
        Assert.Equal(ZipStorer.Compression.Store, entry.Method);
        Assert.Equal(1000L, entry.FileSize);

        using var output = new MemoryStream();
        // Task.Run keeps the copy loop off the test thread: every await inside
        // ExtractFileAsync completes synchronously on a MemoryStream, so the
        // unguarded loop would otherwise spin inline and hang the test runner.
        var extractTask = Task.Run(() => zip.ExtractFileAsync(entry, output));

        // The extract must complete (by succeeding or throwing) within the
        // timeout. Without the EOF guard in the copy loop, ReadAsync returns
        // 0 forever and this task never finishes.
        var completed = await Task.WhenAny(extractTask, Task.Delay(TimeSpan.FromSeconds(10))) == extractTask;
        Assert.True(completed, "ExtractFileAsync did not complete within 10 seconds (infinite hang on truncated archive)");

        await Assert.ThrowsAsync<InvalidDataException>(() => extractTask);
    }

    [Fact]
    public async Task ExtractFileAsync_rejects_truncated_deflate_entry_and_disposes_the_inflater()
    {
        // Same truncated-FileSize scenario as the Store test above, but with a
        // Deflate entry: the EOF throw happens while the copy loop reads through
        // a DeflateStream wrapper, which must be disposed by the throw path
        // instead of leaked (it is created with leaveOpen so the underlying
        // archive stream must stay usable afterwards).
        var archiveBytes = CreateTruncatedDeflateZip();

        var stream = new MemoryStream(archiveBytes);
        using var zip = ZipStorer.Open(stream, FileAccess.Read);
        var entry = Assert.Single(zip.ReadCentralDir());
        Assert.Equal(ZipStorer.Compression.Deflate, entry.Method);
        Assert.Equal(1000L, entry.FileSize);

        using var output = new MemoryStream();
        await Assert.ThrowsAsync<InvalidDataException>(() => zip.ExtractFileAsync(entry, output));

        // the archive stream was handed to the DeflateStream with leaveOpen:
        // it must still be open and usable after the failed extraction
        Assert.True(stream.CanRead);
        _ = stream.Seek(0, SeekOrigin.Begin);
        Assert.Equal(archiveBytes[0], stream.ReadByte());
    }

    static byte[] CreateTruncatedDeflateZip()
    {
        // deflate-compressed payload "hello"; the central directory lies about
        // the uncompressed size (1000) so the copy loop hits EOF
        var payload = Encoding.UTF8.GetBytes("hello");
        var compressed = new MemoryStream();
        using (var deflater = new DeflateStream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            deflater.Write(payload, 0, payload.Length);
        }
        var compressedBytes = compressed.ToArray();

        using var archive = new MemoryStream();
        using var writer = new BinaryWriter(archive, Encoding.UTF8, leaveOpen: true);

        // local file header
        writer.Write(0x04034b50u);         // local file header signature
        writer.Write((ushort)20);          // version needed to extract
        writer.Write((ushort)0);           // general purpose bit flag
        writer.Write((ushort)8);           // compression method: deflate
        writer.Write((ushort)0);           // last mod file time
        writer.Write((ushort)0x21);        // last mod file date (1980-01-01)
        writer.Write(0u);                  // crc-32
        writer.Write((uint)compressedBytes.Length); // compressed size
        writer.Write(1000u);               // uncompressed size (lies)
        writer.Write((ushort)1);           // filename length
        writer.Write((ushort)0);           // extra field length
        writer.Write((byte)'a');            // filename
        writer.Write(compressedBytes);      // file data

        var centralDirOffset = (uint)archive.Length;

        // central directory record
        writer.Write(0x02014b50u);         // central directory signature
        writer.Write((ushort)20);          // version made by
        writer.Write((ushort)20);          // version needed to extract
        writer.Write((ushort)0);           // general purpose bit flag
        writer.Write((ushort)8);           // compression method: deflate
        writer.Write((ushort)0);           // last mod file time
        writer.Write((ushort)0x21);        // last mod file date (1980-01-01)
        writer.Write(0u);                  // crc-32
        writer.Write((uint)compressedBytes.Length); // compressed size
        writer.Write(1000u);               // uncompressed size (lies)
        writer.Write((ushort)1);           // filename length
        writer.Write((ushort)0);           // extra field length
        writer.Write((ushort)0);           // file comment length
        writer.Write((ushort)0);           // disk number start
        writer.Write((ushort)0);           // internal file attributes
        writer.Write(0u);                  // external file attributes
        writer.Write(0u);                  // relative offset of local header
        writer.Write((byte)'a');            // filename

        var centralDirSize = (uint)archive.Length - centralDirOffset;

        // end of central directory record
        writer.Write(0x06054b50u);         // end of central dir signature
        writer.Write((ushort)0);           // number of this disk
        writer.Write((ushort)0);           // disk with start of central directory
        writer.Write((ushort)1);           // entries on this disk
        writer.Write((ushort)1);           // total entries
        writer.Write(centralDirSize);      // size of central directory
        writer.Write(centralDirOffset);    // offset of central directory
        writer.Write((ushort)0);           // zipfile comment length

        return archive.ToArray();
    }
}
