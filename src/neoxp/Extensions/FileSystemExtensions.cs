using System;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
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
        public static (Neo.BlockchainToolkit.Models.ExpressChain chain, string path) LoadExpressChainInfo(this IFileSystem fileSystem, string path)
        {
            path = fileSystem.ResolveExpressFileName(path);
            if (!fileSystem.File.Exists(path))
            {
                throw new Exception($"{path} file doesn't exist");
            }

            var chain = fileSystem.LoadChain(path);

            // validate neo-express file by ensuring stored node zero default account SignatureRedeemScript matches a generated script
            var account = chain.ConsensusNodes[0].Wallet.DefaultAccount ?? throw new InvalidOperationException("consensus node 0 missing default account");
            var keyPair = new Neo.Wallets.KeyPair(Convert.FromHexString(account.PrivateKey));
            var contractScript = Convert.FromHexString(account.Contract.Script);

            if (!Contract.CreateSignatureRedeemScript(keyPair.PublicKey).AsSpan().SequenceEqual(contractScript))
            {
                throw new Exception("Invalid Signature Redeem Script. Was this neo-express file created before RC1?");
            }

            return (chain, path);
        }

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

        // public static string GetNodePath(this IFileSystem fileSystem, ExpressConsensusNode node)
        // {
        //     ArgumentNullException.ThrowIfNull(node);
        //     ArgumentNullException.ThrowIfNull(node.Wallet);

        //     var account = node.Wallet.Accounts.Single(a => a.IsDefault);

        //     var rootPath = fileSystem.Path.Combine(
        //         Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify),
        //         "Neo-Express",
        //         "blockchain-nodes");
        //     return fileSystem.Path.Combine(rootPath, account.ScriptHash);
        // }

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
