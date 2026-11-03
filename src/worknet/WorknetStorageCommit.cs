// Copyright (C) 2015-2026 The Neo Project.
//
// WorknetStorageCommit.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.IO;
using System.IO.Abstractions;

namespace NeoWorkNet;

static class WorknetStorageCommit
{
    // Swaps a staged worknet file + data directory into place. Existing items are only moved
    // aside (to the backup paths), never deleted. This is NOT atomic across the multiple
    // filesystem operations; on failure a best-effort rollback is performed and any rollback
    // failure is reported together with the original error.
    public static void Commit(IFileSystem fs, bool force, string filename, string dataDir,
        string tempFile, string tempData, string backupFile, string backupData)
    {
        var movedData = false;
        var movedFile = false;
        var placedData = false;
        try
        {
            if (fs.Directory.Exists(dataDir))
            {
                if (!force)
                    throw new Exception($"{dataDir} already exists");
                if (fs.DirectoryInfo.New(dataDir).Attributes.HasFlag(FileAttributes.ReparsePoint))
                    throw new Exception($"{dataDir} is a symbolic link or reparse point and will not be replaced");
                fs.Directory.Move(dataDir, backupData);
                movedData = true;
            }

            if (fs.File.Exists(filename))
            {
                fs.File.Move(filename, backupFile);
                movedFile = true;
            }

            fs.Directory.Move(tempData, dataDir);
            placedData = true;
            fs.File.Move(tempFile, filename);
        }
        catch (Exception original)
        {
            var errors = new List<Exception> { original };
            Rollback(fs, filename, dataDir, backupFile, backupData, movedData, movedFile, placedData, errors);
            if (errors.Count == 1)
                throw;
            throw new AggregateException(
                $"Worknet replacement failed and rollback was incomplete. Previous data may remain at {backupData} and {backupFile}.",
                errors);
        }
    }

    static void Rollback(IFileSystem fs, string filename, string dataDir, string backupFile, string backupData,
        bool movedData, bool movedFile, bool placedData, List<Exception> errors)
    {
        if (placedData)
            Attempt(() => fs.Directory.Delete(dataDir, true), errors);
        if (movedFile)
        {
            Attempt(() =>
            {
                if (fs.File.Exists(filename))
                    fs.File.Delete(filename);
                fs.File.Move(backupFile, filename);
            }, errors);
        }
        if (movedData)
            Attempt(() => fs.Directory.Move(backupData, dataDir), errors);
    }

    static void Attempt(Action action, List<Exception> errors)
    {
        try { action(); }
        catch (Exception ex) { errors.Add(ex); }
    }
}
