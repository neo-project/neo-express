// Copyright (C) 2015-2026 The Neo Project.
//
// RocksDbUtilityRestoreCheckpointTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit.Persistence;
using Neo.Wallets;
using System;
using System.IO;
using System.IO.Compression;
using Xunit;

namespace test.bctklib
{
    public class RocksDbUtilityRestoreCheckpointTests
    {
        // These tests exercise malformed-checkpoint-archive handling. Every failure path
        // covered here (ADDRESS metadata lookup/parse and zip entry extraction) happens
        // before any RocksDb database is opened, so no rocksdb native library is required.

        static readonly byte AddressVersion = ProtocolSettings.Default.AddressVersion;
        static readonly string ValidAddress = new UInt160(new byte[20]).ToAddress(AddressVersion);
        static readonly string ValidMetadata = $"12345\n{AddressVersion}\n{ValidAddress}\n";

        // An archive whose ADDRESS metadata entry has been renamed or removed must be
        // reported as an invalid checkpoint archive rather than surfacing the raw
        // InvalidOperationException thrown by the entry lookup.
        [Fact]
        public void RestoreCheckpoint_reports_a_missing_address_entry()
        {
            AssertInvalidCheckpointArchive(("RENAMED.neo-express", ValidMetadata));
        }

        // An archive containing an entry whose name escapes the restore directory
        // (zip-slip) must be reported as an invalid checkpoint archive rather than
        // surfacing the raw IOException thrown by ZipFile.ExtractToDirectory.
        [Fact]
        public void RestoreCheckpoint_reports_a_zip_slip_entry_name()
        {
            AssertInvalidCheckpointArchive(
                ("ADDRESS.neo-express", ValidMetadata),
                ("../escape.txt", "escaped"));
        }

        // An archive containing an entry whose name ends in a directory separator
        // character but contains data must be reported as an invalid checkpoint archive
        // rather than surfacing the raw IOException thrown during extraction.
        [Fact]
        public void RestoreCheckpoint_reports_an_entry_name_ending_in_directory_separator()
        {
            AssertInvalidCheckpointArchive(
                ("ADDRESS.neo-express", ValidMetadata),
                ("data-dir/", "data"));
        }

        // An archive containing an entry whose name exceeds the maximum path length
        // must be reported as an invalid checkpoint archive rather than surfacing the
        // raw IOException thrown by the FileStream constructor during extraction.
        [Fact]
        public void RestoreCheckpoint_reports_an_over_long_entry_name()
        {
            AssertInvalidCheckpointArchive(
                ("ADDRESS.neo-express", ValidMetadata),
                (new string('z', 300) + ".txt", "data"));
        }

        // RestoreCheckpoint must wrap every malformed-archive failure (including
        // IOException/InvalidOperationException) in the friendly
        // "not a valid checkpoint archive" exception.
        static void AssertInvalidCheckpointArchive(params (string entryName, string content)[] entries)
        {
            var archivePath = Path.Combine(Path.GetTempPath(), $"neo-express-bad-checkpoint-{Guid.NewGuid():N}.neoxp-checkpoint");
            // restorePath lives inside a dedicated parent directory so that a zip-slip
            // entry (../escape.txt) lands inside the parent rather than in the temp root
            // and can be cleaned up with it.
            var parentPath = Path.Combine(Path.GetTempPath(), $"neo-express-restore-{Guid.NewGuid():N}");
            var restorePath = Path.Combine(parentPath, "restore");
            try
            {
                using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
                {
                    foreach (var (entryName, content) in entries)
                    {
                        var entry = archive.CreateEntry(entryName);
                        using var writer = new StreamWriter(entry.Open());
                        writer.Write(content);
                    }
                }

                var act = () => RocksDbUtility.RestoreCheckpoint(archivePath, restorePath);

                act.Should().Throw<Exception>().WithMessage("*not a valid checkpoint archive*");
            }
            finally
            {
                if (File.Exists(archivePath))
                    File.Delete(archivePath);
                if (Directory.Exists(parentPath))
                    Directory.Delete(parentPath, true);
            }
        }
    }
}
