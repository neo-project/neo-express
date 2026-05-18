// Copyright (C) 2015-2026 The Neo Project.
//
// CheckpointFixture.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;
using System.IO.Abstractions;

namespace NeoTestHarness
{
    public abstract class CheckpointFixture : IDisposable
    {
        readonly static Lazy<IFileSystem> defaultFileSystem = new Lazy<IFileSystem>(() => new FileSystem());
        readonly CheckpointStore checkpointStore;

        public ICheckpointStore CheckpointStore => checkpointStore;
        public ProtocolSettings ProtocolSettings => checkpointStore.Settings;

        public CheckpointFixture(string checkpointPath)
        {
            if (Path.IsPathFullyQualified(checkpointPath))
            {
                if (!File.Exists(checkpointPath))
                    throw new FileNotFoundException("couldn't find checkpoint", checkpointPath);
            }
            else
            {
                var originalCheckpointPath = checkpointPath;
                var directory = Path.GetFullPath(".");
                while (true)
                {
                    var tempPath = Path.GetFullPath(checkpointPath, directory);
                    if (File.Exists(tempPath))
                    {
                        checkpointPath = tempPath;
                        break;
                    }

                    var parentDirectory = Path.GetDirectoryName(directory);
                    if (parentDirectory is null)
                        throw new FileNotFoundException("couldn't find checkpoint", originalCheckpointPath);

                    directory = parentDirectory;
                }
            }

            checkpointStore = new CheckpointStore(checkpointPath);
        }

        public void Dispose()
        {
            checkpointStore.Dispose();
        }

        public ExpressChain FindChain(string fileName = Constants.DEFAULT_EXPRESS_FILENAME, IFileSystem? fileSystem = null, string? searchFolder = null)
            => (fileSystem ?? defaultFileSystem.Value).FindChain(fileName, searchFolder);
    }
}
