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
}
