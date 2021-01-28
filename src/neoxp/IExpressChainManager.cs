using System;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Neo.BlockchainToolkit.Persistence;
using Neo.Network.RPC;
using NeoExpress.Models;
using Newtonsoft.Json;
using Nito.Disposables;

namespace NeoExpress
{
    internal interface IExpressChainManager
    {
        ExpressChain Chain { get; }
        void SaveChain(string path);
        Task<(string path, bool online)> CreateCheckpointAsync(string checkPointPath, bool force);
        void RestoreCheckpoint(string checkPointPath, bool force);
        void ResetNode(ExpressConsensusNode node, bool force);
    }

    internal class ExpressChainManager : IExpressChainManager
    {
        readonly IFileSystem fileSystem;
        readonly ExpressChain chain;

        public ExpressChainManager(IFileSystem fileSystem, ExpressChain chain)
        {
            this.fileSystem = fileSystem;
            this.chain = chain;
        }

        public ExpressChain Chain => chain;

        private const string CHECKPOINT_EXTENSION = ".nxp3-checkpoint";

        internal string ResolveCheckpointFileName(string path) => fileSystem.ResolveFileName(path, CHECKPOINT_EXTENSION, () => $"{DateTimeOffset.Now:yyyyMMdd-hhmmss}");

        public async Task<(string path, bool online)> CreateCheckpointAsync(string checkPointPath, bool force)
        {
            if (chain.ConsensusNodes.Count != 1)
            {
                throw new ArgumentException("Checkpoint create is only supported on single node express instances", nameof(chain));
            }

            checkPointPath = ResolveCheckpointFileName(checkPointPath);
            if (fileSystem.File.Exists(checkPointPath))
            {
                if (force)
                {
                    fileSystem.File.Delete(checkPointPath);
                }
                else
                {
                    throw new Exception("You must specify --force to overwrite an existing file");
                }
            }

            var parentPath = fileSystem.Path.GetDirectoryName(checkPointPath);
            if (!fileSystem.Directory.Exists(parentPath))
            {
                fileSystem.Directory.CreateDirectory(parentPath);
            }

            var node = chain.ConsensusNodes[0];
            if (node.IsRunning())
            {
                var uri = chain.GetUri();
                var rpcClient = new RpcClient(uri.ToString());
                await rpcClient.RpcSendAsync("expresscreatecheckpoint", checkPointPath).ConfigureAwait(false);
                return (checkPointPath, true);
            }
            else
            {
                var multiSigAccount = node.Wallet.Accounts.Single(a => a.IsMultiSigContract());
                using var db = RocksDbStore.Open(fileSystem.GetNodePath(node));
                db.CreateCheckpoint(checkPointPath, chain.Magic, multiSigAccount.ScriptHash);
                return (checkPointPath, false);
            }
        }

        public void RestoreCheckpoint(string checkPointArchive, bool force)
        {
            if (chain.ConsensusNodes.Count != 1)
            {
                throw new ArgumentException("Checkpoint restore is only supported on single node express instances", nameof(chain));
            }

            checkPointArchive = ResolveCheckpointFileName(checkPointArchive);
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

            var checkpointTempPath = fileSystem.GetTempFolder();
            using var folderCleanup = AnonymousDisposable.Create(() =>
            {
                if (fileSystem.Directory.Exists(checkpointTempPath))
                {
                    fileSystem.Directory.Delete(checkpointTempPath, true);
                }
            });

            var multiSigAccount = node.Wallet.Accounts.Single(a => a.IsMultiSigContract());
            var nodeFolder = fileSystem.GetNodePath(node);
            if (fileSystem.Directory.Exists(nodeFolder))
            {
                if (force)
                {
                    fileSystem.Directory.Delete(nodeFolder, true);
                }
                else
                {
                    throw new Exception("You must specify force to restore a checkpoint to an existing blockchain.");
                }
            }

            RocksDbStore.RestoreCheckpoint(checkPointArchive, checkpointTempPath, chain.Magic, multiSigAccount.ScriptHash);
            fileSystem.Directory.Move(checkpointTempPath, nodeFolder);
        }

        public void SaveChain(string path)
        {
            var serializer = new JsonSerializer();
            using (var stream = fileSystem.File.Open(path, System.IO.FileMode.Create, System.IO.FileAccess.Write))
            using (var writer = new JsonTextWriter(new System.IO.StreamWriter(stream)) { Formatting = Formatting.Indented })
            {
                serializer.Serialize(writer, chain);
            }
        }

        public void ResetNode(ExpressConsensusNode node, bool force)
        {
            if (node.IsRunning())
            {
                var scriptHash = node.Wallet.DefaultAccount?.ScriptHash ?? "<unknown>";
                throw new InvalidOperationException($"node {scriptHash} currently running");
            }

            var folder = fileSystem.GetNodePath(node);
            if (fileSystem.Directory.Exists(folder))
            {
                if (!force)
                {
                    throw new InvalidOperationException("--force must be specified when resetting a node");
                }

                fileSystem.Directory.Delete(folder, true);
            }
        }
    }
}
