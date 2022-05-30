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

            public Restore(CommandLineApplication app) : this(app.GetExpressFile())
            {
            }

            [Argument(0, "Checkpoint file name")]
            [Required]
            internal string Name { get; init; } = string.Empty;

            [Option(Description = "Overwrite existing data")]
            internal bool Force { get; }

            internal int OnExecute(CommandLineApplication app) => app.Execute(this.Execute);

            internal void Execute(IFileSystem fileSystem, IConsole console)
            {
                if (chain.ConsensusNodes.Count != 1)
                {
                    throw new ArgumentException("Checkpoint restore is only supported on single node express instances", nameof(Restore.chain));
                }

                var checkPointArchive = fileSystem.ResolveCheckpointFileName(Name);
                if (!fileSystem.File.Exists(checkPointArchive))
                {
                    throw new Exception($"Checkpoint {checkPointArchive} couldn't be found");
                }

                var node = chain.ConsensusNodes[0];
                if (node.IsRunning())
                {
                    var address = node.Wallet.DefaultAccount?.ScriptHash ?? "<unknown>";
                    throw new InvalidOperationException($"node {address} currently running");
                }

                string checkpointTempPath;
                do
                {
                    checkpointTempPath = fileSystem.Path.Combine(
                        fileSystem.Path.GetTempPath(),
                        fileSystem.Path.GetRandomFileName());
                }
                while (fileSystem.Directory.Exists(checkpointTempPath));
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
                    if (!Force)
                    {
                        throw new Exception("You must specify force to restore a checkpoint to an existing blockchain.");
                    }

                    fileSystem.Directory.Delete(nodePath, true);
                }

                var settings = chain.GetProtocolSettings();
                var scriptHash = node.Wallet.DefaultAccount?.GetScriptHash() 
                    ?? throw new Exception("Consensus wallet is missing a default account");
                RocksDbUtility.RestoreCheckpoint(checkPointArchive, checkpointTempPath,
                    settings.Network, settings.AddressVersion, scriptHash);
                fileSystem.Directory.Move(checkpointTempPath, nodePath);

                console.WriteLine($"Checkpoint {fileSystem.Path.GetFileName(checkPointArchive)} successfully restored");
            }
        }
    }
}
