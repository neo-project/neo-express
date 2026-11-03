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
    //
    // Without force nothing existing is ever replaced. The upfront checks give clear errors; the
    // moves below do not overwrite either (Directory.Move/File.Move throw if the target exists),
    // so an item created concurrently between the check and the move is not silently replaced.
    public static void Commit(IFileSystem fs, bool force, string filename, string dataDir,
        string tempFile, string tempData, string backupFile, string backupData)
    {
        if (!force && fs.File.Exists(filename))
            throw new IOException($"Worknet configuration '{filename}' already exists");
        if (!force && fs.Directory.Exists(dataDir))
            throw new IOException($"Worknet data directory '{dataDir}' already exists");
        if (fs.Directory.Exists(dataDir) && fs.DirectoryInfo.New(dataDir).Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new IOException($"{dataDir} is a symbolic link or reparse point and will not be replaced");

        var movedData = false;
        var movedFile = false;
        var placedData = false;
        try
        {
            if (fs.Directory.Exists(dataDir))
            {
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

    // Backups left behind by a replacement that was interrupted (process killed) or whose rollback failed.
    // They are never deleted or restored automatically since they may hold the only copy of valid data.
    public static IReadOnlyList<string> FindLeftoverBackups(IFileSystem fs, string filename, string dataDir)
    {
        var result = new List<string>();
        CollectBackups(fs, fs.Path.GetFullPath(filename), result);
        CollectBackups(fs, fs.Path.GetFullPath(dataDir), result);
        return result;
    }

    public static string DescribeLeftoverBackups(IEnumerable<string> backups, string filename, string dataDir)
        => $"A previous replacement of {filename} appears to have been interrupted. Backups of the previous worknet were found: " +
           $"{string.Join(", ", backups)}. They have not been modified. To restore, stop any running worknet and move the backup " +
           $"directory back to {dataDir} and the backup file back to {filename} (replacing the current ones). " +
           "If you no longer need the previous worknet, delete the backups manually.";

    static void CollectBackups(IFileSystem fs, string path, List<string> result)
    {
        var dir = fs.Path.GetDirectoryName(path);
        if (dir is null || !fs.Directory.Exists(dir))
            return;
        var prefix = fs.Path.GetFileName(path) + ".";
        foreach (var entry in fs.Directory.EnumerateFileSystemEntries(dir, prefix + "*.bak"))
        {
            var name = fs.Path.GetFileName(entry);
            var id = name.Substring(prefix.Length, name.Length - prefix.Length - ".bak".Length);
            if (id.Length == 32 && id.All(Uri.IsHexDigit))
                result.Add(entry);
        }
    }

    static void Attempt(Action action, List<Exception> errors)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            errors.Add(ex);
        }
    }
}
