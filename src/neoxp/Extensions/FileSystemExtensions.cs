// Copyright (C) 2015-2023 The Neo Project.
//
// FileSystemExtensions.cs is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BlockchainToolkit.Models;
using Neo.IO;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using System.IO.Abstractions;

namespace NeoExpress
{
    static class FileSystemExtensions
    {
        public static string ResolveFileName(this IFileSystem fileSystem, string fileName, string extension, Func<string> getDefaultFileName)
        {
            var isDefaultPath = false;
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = getDefaultFileName();
                isDefaultPath = true;
            }

            if (!fileSystem.Path.IsPathFullyQualified(fileName))
            {
                if (isDefaultPath)
                {
                    var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);
                    var folder = Path.Combine(homeDir, ".neo-express");
                    if (fileSystem.Path.Exists(folder) == false)
                        fileSystem.Directory.CreateDirectory(folder);
                    fileName = fileSystem.Path.Combine(folder, fileName);
                }
                else
                    fileName = fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), fileName);
            }

            return extension.Equals(fileSystem.Path.GetExtension(fileName), StringComparison.OrdinalIgnoreCase)
                ? fileName : fileName + extension;
        }

        public static string GetTempFolder(this IFileSystem fileSystem)
        {
            string path;
            do
            {
                path = fileSystem.Path.Combine(
                    fileSystem.Path.GetTempPath(),
                    fileSystem.Path.GetRandomFileName());
            }
            while (fileSystem.Directory.Exists(path));
            return path;
        }

        public static string GetNodePath(this IFileSystem fileSystem, ExpressConsensusNode node)
        {
            ArgumentNullException.ThrowIfNull(node);
            ArgumentNullException.ThrowIfNull(node.Wallet);

            var account = node.Wallet.Accounts.Single(a => a.IsDefault);

            var rootPath = fileSystem.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify),
                "Neo-Express",
                "blockchain-nodes");
            return fileSystem.Path.Combine(rootPath, account.ScriptHash);
        }

        public static async Task<(NefFile nefFile, ContractManifest manifest)> LoadContractAsync(this IFileSystem fileSystem, string contractPath)
        {
            var nefTask = LoadNefAsync(fileSystem, contractPath);
            var manifestTask = LoadManifestAsync(fileSystem, contractPath);

            await Task.WhenAll(nefTask, manifestTask).ConfigureAwait(false);
            return (await nefTask, await manifestTask);

            static async Task<NefFile> LoadNefAsync(IFileSystem fileSystem, string contractPath)
            {
                var buffer = await fileSystem.File.ReadAllBytesAsync(contractPath).ConfigureAwait(false);
                return LoadNef(buffer);

            }

            static NefFile LoadNef(ReadOnlyMemory<byte> memory)
            {
                var reader = new MemoryReader(memory);
                return reader.ReadSerializable<NefFile>();
            }

            static async Task<ContractManifest> LoadManifestAsync(IFileSystem fileSystem, string contractPath)
            {
                var path = fileSystem.Path.ChangeExtension(contractPath, ".manifest.json");
                var buffer = await fileSystem.File.ReadAllBytesAsync(path).ConfigureAwait(false);
                return ContractManifest.Parse(buffer);
            }
        }
    }
}
