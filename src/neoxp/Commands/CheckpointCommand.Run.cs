using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using NeoExpress.Node;
using Nito.Disposables;

namespace NeoExpress.Commands
{
    partial class CheckpointCommand
    {
        [Command("run", Description = "Run a neo-express checkpoint (discarding changes on shutdown)")]
        internal class Run
        {
            readonly IExpressChain expressFile;

            public Run(IExpressChain expressFile)
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
                if (expressFile.ConsensusNodes.Count != 1)
                {
                    throw new ArgumentException("Checkpoint create is only supported on single node express instances", nameof(chain));
                }

                var checkPointPath = fileSystem.ResolveCheckpointFileName(Name);
                if (!fileSystem.File.Exists(checkPointPath))
                {
                    throw new Exception($"Checkpoint {Name} couldn't be found");
                }

                // TODO: expressFile.Chain.GetGenesisScriptHash
                // TODO: Node.ExpressSystem expressFile.Chain
                using var expressStorage = CheckpointExpressStorage.OpenCheckpoint(
                    checkPointPath, expressFile.Network, expressFile.AddressVersion, expressFile.Chain.GetGenesisScriptHash());
                var expressSystem = new Node.ExpressSystem(
                    expressFile.Chain, expressFile.ConsensusNodes[0], expressStorage, console, Trace, SecondsPerBlock);
                await expressSystem.RunAsync(token);
            }
        }
    }
}
