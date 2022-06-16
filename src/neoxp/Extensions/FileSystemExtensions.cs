using System;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.IO;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;

using static Neo.BlockchainToolkit.Constants;

namespace NeoExpress
{
    static class FileSystemExtensions
    {
        public static IExpressChain GetExpressFile(this CommandLineApplication app)
        {
            var option = app.GetOptions().Single(o => o.LongName == "input");
            var input = option.Value() ?? string.Empty;
            var fileSystem = app.GetRequiredService<IFileSystem>();
            return new ExpressChainImpl(input, fileSystem);
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


        public static string Resolve(this System.IO.Abstractions.IDirectoryInfo @this, string path)
            => @this.FileSystem.Path.IsPathFullyQualified(path)
                ? path
                : @this.FileSystem.Path.Combine(@this.FullName, path);

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
