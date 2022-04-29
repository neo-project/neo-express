using System;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Neo.IO;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.Wallets;
using NeoExpress.Models;
using Nito.Disposables;
using static Neo.BlockchainToolkit.Constants;

namespace NeoExpress
{
    static class FileSystemExtensions
    {
        public static (ExpressChain chain, string path) LoadExpressChain(this IFileSystem fileSystem, string path)
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

        public static void ResetNode(this IFileSystem fileSystem, ExpressConsensusNode node, bool force)
        {
            if (node.IsRunning())
            {
                var scriptHash = node.Wallet.DefaultAccount?.ScriptHash ?? "<unknown>";
                throw new InvalidOperationException($"node {scriptHash} currently running");
            }

            var nodePath = fileSystem.GetNodePath(node);
            if (fileSystem.Directory.Exists(nodePath))
            {
                if (!force)
                {
                    throw new InvalidOperationException("--force must be specified when resetting a node");
                }

                fileSystem.Directory.Delete(nodePath, true);
            }
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

        public static async Task<(string path, IExpressNode.CheckpointMode checkpointMode)>
            CreateCheckpointAsync(this IFileSystem fileSystem, IExpressNode expressNode, string checkpointPath, bool force)
        {
            if (expressNode.Chain.ConsensusNodes.Count != 1)
            {
                throw new ArgumentException("Checkpoint create is only supported on single node express instances", nameof(expressNode));
            }

            checkpointPath = fileSystem.ResolveCheckpointFileName(checkpointPath);
            if (fileSystem.File.Exists(checkpointPath))
            {
                if (force)
                {
                    fileSystem.File.Delete(checkpointPath);
                }
                else
                {
                    throw new Exception("You must specify --force to overwrite an existing file");
                }
            }

            var parentPath = fileSystem.Path.GetDirectoryName(checkpointPath);
            if (!fileSystem.Directory.Exists(parentPath))
            {
                fileSystem.Directory.CreateDirectory(parentPath);
            }

            var mode = await expressNode.CreateCheckpointAsync(checkpointPath).ConfigureAwait(false);

            return (checkpointPath, mode);
        }

        internal static string RestoreCheckpoint(this IFileSystem fileSystem, ExpressChain chain, string checkPointArchive, bool force)
        {
            if (chain.ConsensusNodes.Count != 1)
            {
                throw new ArgumentException("Checkpoint restore is only supported on single node express instances", nameof(chain));
            }

            checkPointArchive = fileSystem.ResolveCheckpointFileName(checkPointArchive);
            if (!fileSystem.File.Exists(checkPointArchive))
            {
                throw new Exception($"Checkpoint {checkPointArchive} couldn't be found");
            }

            var node = chain.ConsensusNodes[0];
            if (node.IsRunning())
            {
                var scriptHash = node.Wallet.DefaultAccount?.ScriptHash ?? "<unknown>";
                throw new InvalidOperationException($"node {scriptHash} currently running");
            }

            string checkpointTempPath;
            do
            {
                checkpointTempPath = fileSystem.Path.Combine(
                    fileSystem.Path.GetTempPath(),
                    fileSystem.Path.GetRandomFileName());
            }
            while (fileSystem.Directory.Exists(checkpointTempPath));
            using var folderCleanup = Nito.Disposables.AnonymousDisposable.Create(() =>
            {
                if (fileSystem.Directory.Exists(checkpointTempPath))
                {
                    fileSystem.Directory.Delete(checkpointTempPath, true);
                }
            });

            var nodePath = fileSystem.GetNodePath(node);
            if (fileSystem.Directory.Exists(nodePath))
            {
                if (!force)
                {
                    throw new Exception("You must specify force to restore a checkpoint to an existing blockchain.");
                }

                fileSystem.Directory.Delete(nodePath, true);
            }

            var settings = chain.GetProtocolSettings();
            var wallet = Models.DevWallet.FromExpressWallet(settings, node.Wallet);
            var multiSigAccount = wallet.GetMultiSigAccounts().Single();
            RocksDbUtility.RestoreCheckpoint(checkPointArchive, checkpointTempPath,
                settings.Network, settings.AddressVersion, multiSigAccount.ScriptHash);
            fileSystem.Directory.Move(checkpointTempPath, nodePath);

            return checkPointArchive;
        }

        public static bool TryImportNEP6(this IFileSystem fileSystem, string path, string password, Neo.ProtocolSettings settings, [MaybeNullWhen(false)] out DevWallet wallet)
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

            if (fileSystem.File.Exists(path)) throw new System.IO.IOException();

            using var stream = fileSystem.File.OpenWrite(path);
            using var textWriter = new System.IO.StreamWriter(stream);
            using var writer = new Newtonsoft.Json.JsonTextWriter(textWriter);
            var scrypt = Neo.Wallets.NEP6.ScryptParameters.Default;

            using var _ = writer.WriteStartObjectAuto();

            writer.WriteProperty("name", wallet.Name);
            writer.WriteProperty("version", "1.0");
            writer.WritePropertyNull("extra");
            writer.WritePropertyName("scrypt");
            {
                using var __ = writer.WriteStartObjectAuto();
                writer.WriteProperty("n", scrypt.N);
                writer.WriteProperty("r", scrypt.R);
                writer.WriteProperty("p", scrypt.P);
            }
            writer.WritePropertyName("accounts");
            {
                using var __ = writer.WriteStartArrayAuto();

                foreach (var account in wallet.Accounts)
                {
                    var privateKey = Convert.FromHexString(account.PrivateKey);
                    var keyPair = new KeyPair(privateKey);
                    var exportedKey = keyPair.Export(password, addressVersion, scrypt.N, scrypt.R, scrypt.P);
                    var script = account.Contract.Script.HexToBytes();

                    using var _3 = writer.WriteStartObjectAuto();

                    writer.WriteProperty("address", account.ScriptHash);
                    writer.WriteProperty("label", account.Label);
                    writer.WriteProperty("isDefault", account.IsDefault);
                    writer.WriteProperty("lock", false);
                    writer.WriteProperty("key", exportedKey);
                    writer.WritePropertyNull("extra");

                    writer.WritePropertyName("contract");
                    using var _4 = writer.WriteStartObjectAuto();

                    writer.WriteProperty("script", Convert.ToBase64String(script));
                    writer.WriteProperty("deployed", false);

                    writer.WritePropertyName("parameters");
                    using var _5 = writer.WriteStartArrayAuto();
                    for (int i = 0; i < account.Contract.Parameters.Count; i++)
                    {
                        var paramType = account.Contract.Parameters[i];

                        using var _6 = writer.WriteStartObjectAuto();

                        writer.WriteProperty("name", $"{paramType}{i}");
                        writer.WriteProperty("type", paramType);
                    }
                }
            }
        }
    }
}
