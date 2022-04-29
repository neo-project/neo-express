using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Linq;
using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;

namespace NeoExpress.Commands
{
    partial class CheckpointCommand
    {
        [Command("restore", Description = "Restore a neo-express checkpoint")]
        internal class Restore
        {
            readonly IExpressFile expressFile;

            public Restore(IExpressFile expressFile)
            {
                this.expressFile = expressFile;
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
                var checkpointPath = RestoreCheckpoint(fileSystem, expressFile.Chain, Name, Force);
                console.WriteLine($"Checkpoint {fileSystem.Path.GetFileName(checkpointPath)} successfully restored");
            }

            internal static string RestoreCheckpoint(IFileSystem fileSystem, ExpressChain chain, string checkPointArchive, bool force)
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
                Neo.BlockchainToolkit.Persistence.RocksDbUtility.RestoreCheckpoint(checkPointArchive, checkpointTempPath,
                    settings.Network, settings.AddressVersion, multiSigAccount.ScriptHash);
                fileSystem.Directory.Move(checkpointTempPath, nodePath);

                return checkPointArchive;
            }

        }
    }
}
