using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Nito.Disposables;

namespace NeoExpress.Commands
{
    partial class CheckpointCommand
    {
        [Command("run", Description = "Run a neo-express checkpoint (discarding changes on shutdown)")]
        internal class Run
        {
            readonly IExpressFile expressFile;

            public Run(IExpressFile expressFile)
            {
                this.expressFile = expressFile;
            }

            public Run(CommandLineApplication app) : this(app.GetExpressFile())
            {
            }

            [Argument(0, "Checkpoint file name")]
            [Required]
            internal string Name { get; init; } = string.Empty;

            [Option(Description = "Time between blocks")]
            internal uint SecondsPerBlock { get; }

            [Option(Description = "Enable contract execution tracing")]
            internal bool Trace { get; init; } = false;

            internal Task<int> OnExecuteAsync(CommandLineApplication app, CancellationToken token)
                => app.ExecuteAsync(this.ExecuteAsync, token);

            internal async Task ExecuteAsync(IFileSystem fileSystem, IConsole console, CancellationToken token)
            {
                var chain = expressFile.Chain;
                if (chain.ConsensusNodes.Count != 1)
                {
                    throw new ArgumentException("Checkpoint create is only supported on single node express instances", nameof(chain));
                }

                var storageProvider = GetCheckpointStorageProvider(chain, fileSystem, Name);
                using var disposable = storageProvider as IDisposable ?? NoopDisposable.Instance;

                await Node.NodeUtility.RunAsync(chain, storageProvider, chain.ConsensusNodes[0], Trace, console, SecondsPerBlock, token);
            }

            internal static Neo.Plugins.IStorageProvider GetCheckpointStorageProvider(ExpressChain chain, IFileSystem fileSystem, string checkPointPath)
            {
                if (chain.ConsensusNodes.Count != 1)
                {
                    throw new ArgumentException("Checkpoint restore is only supported on single node express instances", nameof(chain));
                }

                var node = chain.ConsensusNodes[0];
                if (node.IsRunning()) throw new Exception($"node already running");

                checkPointPath = fileSystem.ResolveCheckpointFileName(checkPointPath);
                if (!fileSystem.File.Exists(checkPointPath))
                {
                    throw new Exception($"Checkpoint {checkPointPath} couldn't be found");
                }

                var contract = chain.CreateGenesisContract();
                return CheckpointStorageProvider.Open(checkPointPath, chain.Network, chain.AddressVersion, contract.ScriptHash);
            }
        }
    }
}
