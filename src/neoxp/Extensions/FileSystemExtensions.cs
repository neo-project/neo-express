using System;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Neo.BlockchainToolkit.Models;
using Neo.IO;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using static Neo.BlockchainToolkit.Constants;

namespace NeoExpress
{
    static class FileSystemExtensions
    {
        public static string ResolveExpressFileName(this IFileSystem fileSystem, string path)
            => fileSystem.ResolveFileName(path, EXPRESS_EXTENSION, () => DEFAULT_EXPRESS_FILENAME);

        public static string ResolveFileName(this IFileSystem fileSystem, string fileName, string extension, Func<string> getDefaultFileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = getDefaultFileName();
            }

            if (!fileSystem.Path.IsPathFullyQualified(fileName))
            {
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
            var nefTask = Task.Run(() =>
            {
                using var stream = fileSystem.File.OpenRead(contractPath);
                using var reader = new System.IO.BinaryReader(stream, System.Text.Encoding.UTF8, false);
                return reader.ReadSerializable<NefFile>();
            });

            var manifestTask = fileSystem.File.ReadAllBytesAsync(fileSystem.Path.ChangeExtension(contractPath, ".manifest.json"))
                .ContinueWith(t => ContractManifest.Parse(t.Result), default, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);

            await Task.WhenAll(nefTask, manifestTask).ConfigureAwait(false);
            return (await nefTask, await manifestTask);
        }
    }
}
