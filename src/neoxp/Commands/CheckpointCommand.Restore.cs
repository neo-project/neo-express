using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Nito.Disposables;

namespace NeoExpress.Commands
{
    partial class CheckpointCommand
    {
        [Command("restore", Description = "Restore a neo-express checkpoint")]
        internal class Restore
        {
            readonly IExpressChain expressFile;

            public Restore(IExpressChain expressFile)
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
                var checkpointPath = Execute(expressFile, Name, Force, fileSystem);
                console.WriteLine($"Checkpoint {fileSystem.Path.GetFileName(checkpointPath)} successfully restored");
            }

            internal static string Execute(IExpressChain expressFile, string checkPointArchive, bool force, IFileSystem fileSystem)
            {
                if (expressFile.ConsensusNodes.Count != 1)
                {
                    throw new ArgumentException("Checkpoint restore is only supported on single node express instances", nameof(chain));
                }

                checkPointArchive = fileSystem.ResolveCheckpointFileName(checkPointArchive);
                if (!fileSystem.File.Exists(checkPointArchive))
                {
                    throw new Exception($"Checkpoint {checkPointArchive} couldn't be found");
                }

                var node = expressFile.ConsensusNodes[0];
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

                // TODO: expressFile.Chain
                var settings = expressFile.Chain.GetProtocolSettings();
                var contract = expressFile.Chain.CreateGenesisContract();
                RocksDbUtility.RestoreCheckpoint(checkPointArchive, checkpointTempPath,
                    settings.Network, settings.AddressVersion, contract.ScriptHash);
                fileSystem.Directory.Move(checkpointTempPath, nodePath);

                return checkPointArchive;
            }
        }
    }
}
