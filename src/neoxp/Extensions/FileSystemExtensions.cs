using System;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.IO;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.Wallets;
using NeoExpress.Models;
using Newtonsoft.Json;

using static Neo.BlockchainToolkit.Constants;
using FileMode = System.IO.FileMode;
using FileAccess = System.IO.FileAccess;
using StreamWriter = System.IO.StreamWriter;


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

        internal const string CHECKPOINT_EXTENSION = ".neoxp-checkpoint";

        public static string ResolveCheckpointFileName(this IFileSystem fileSystem, string path)
            => fileSystem.ResolveFileName(path, CHECKPOINT_EXTENSION, () => $"{DateTimeOffset.Now:yyyyMMdd-hhmmss}");

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

        public static bool TryImportNEP6(this IFileSystem fileSystem, string path, string password, ProtocolSettings settings, [MaybeNullWhen(false)] out DevWallet wallet)
        {
            if (fileSystem.File.Exists(path))
            {
                var json = Neo.IO.Json.JObject.Parse(fileSystem.File.ReadAllBytes(path));
                var nep6wallet = new Neo.Wallets.NEP6.NEP6Wallet(string.Empty, settings, json);
                using var unlock = nep6wallet.Unlock(password);

                wallet = new DevWallet(settings, nep6wallet.Name);
                foreach (var account in nep6wallet.GetAccounts())
                {
                    var devAccount = wallet.CreateAccount(account.Contract, account.GetKey());
                    devAccount.Label = account.Label;
                    devAccount.IsDefault = account.IsDefault;
                }

                return true;
            }

            wallet = null;
            return false;
        }

        public static void ExportNEP6(this IFileSystem fileSystem, ExpressWallet wallet, string path, string password, byte addressVersion)
        {
            // TODO: use NEP6Wallet.ToJson once https://github.com/neo-project/neo/pull/2714 is merged + released

            using var stream = fileSystem.File.Open(path, FileMode.Create, FileAccess.Write);
            using var textWriter = new StreamWriter(stream);
            using var writer = new JsonTextWriter(textWriter);
            var scrypt = Neo.Wallets.NEP6.ScryptParameters.Default;

            using var _ = writer.WriteObject();
            writer.WriteProperty("name", wallet.Name);
            writer.WriteProperty("version", "1.0");
            writer.WritePropertyNull("extra");
            using (var __ = writer.WritePropertyObject("scrypt"))
            {
                writer.WriteProperty("n", scrypt.N);
                writer.WriteProperty("r", scrypt.R);
                writer.WriteProperty("p", scrypt.P);
            }
            using (var __ = writer.WritePropertyArray("accounts"))
            {
                foreach (var account in wallet.Accounts)
                {
                    var privateKey = Convert.FromHexString(account.PrivateKey);
                    var keyPair = new KeyPair(privateKey);
                    var exportedKey = keyPair.Export(password, addressVersion, scrypt.N, scrypt.R, scrypt.P);
                    var script = account.Contract.Script.HexToBytes();

                    using var __a = writer.WriteObject();
                    writer.WriteProperty("address", account.ScriptHash);
                    writer.WriteProperty("label", account.Label);
                    writer.WriteProperty("isDefault", account.IsDefault);
                    writer.WriteProperty("lock", false);
                    writer.WriteProperty("key", exportedKey);
                    writer.WritePropertyNull("extra");

                    using var __c = writer.WritePropertyObject("contract");
                    writer.WriteProperty("script", Convert.ToBase64String(script));
                    writer.WriteProperty("deployed", false);

                    using var __pa = writer.WritePropertyArray("parameters");
                    for (int i = 0; i < account.Contract.Parameters.Count; i++)
                    {
                        var paramType = account.Contract.Parameters[i];

                        using var __p = writer.WriteObject();
                        writer.WriteProperty("name", $"{paramType}{i}");
                        writer.WriteProperty("type", paramType);
                    }
                }
            }
        }
    }
}
