using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit.Persistence;
using Nito.Disposables;

namespace NeoExpress.Commands
{
    partial class CheckpointCommand
    {
        [Command("restore", Description = "Restore a neo-express checkpoint")]
        internal class Restore
        {
            readonly IExpressChain chain;

            public Restore(IExpressChain chain)
            {
                this.chain = chain;
            }

            public Restore(CommandLineApplication app)
            {
                this.chain = app.GetExpressFile();
            }

            [Argument(0, "Checkpoint file name")]
            [Required]
            internal string Name { get; init; } = string.Empty;

            [Option(Description = "Overwrite existing data")]
            internal bool Force { get; }

            internal int OnExecute(CommandLineApplication app) => app.Execute(this.Execute);

            internal void Execute(IFileSystem fileSystem, IConsole console)
            {
                RestoreCheckpoint(chain, fileSystem, Name, Force);
                console.WriteLine($"Checkpoint {Name} successfully restored");
            }

            public static void RestoreCheckpoint(IExpressChain chain, IFileSystem fileSystem, string checkPointArchive, bool force)
            {
                if (chain.ConsensusNodes.Count != 1)
                {
                    throw new ArgumentException("Checkpoint restore is only supported on single node express instances", nameof(chain));
                }

                checkPointArchive = CheckpointCommand.ResolveFileName(fileSystem, checkPointArchive);
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

                var nodePath = fileSystem.GetNodePath(node);
                if (fileSystem.Directory.Exists(nodePath))
                {
                    if (!force)
                    {
                        throw new Exception("You must specify force to restore a checkpoint to an existing blockchain.");
                    }

                    fileSystem.Directory.Delete(nodePath, true);
                }

                var consensusContract = chain.GetConsensusContract();
                RocksDbUtility.RestoreCheckpoint(checkPointArchive, checkpointTempPath,
                    chain.Network, chain.AddressVersion, consensusContract.ScriptHash);
                fileSystem.Directory.Move(checkpointTempPath, nodePath);
            }
        }
    }
}
