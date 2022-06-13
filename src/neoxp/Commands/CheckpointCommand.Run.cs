using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Node;

namespace NeoExpress.Commands
{
    partial class CheckpointCommand
    {
        [Command("run", Description = "Run a neo-express checkpoint (discarding changes on shutdown)")]
        internal class Run
        {
            readonly IExpressChain chain;

            public Run(IExpressChain chain)
            {
                this.chain = chain;
            }

            public Run(CommandLineApplication app)
            {
                this.chain = app.GetExpressFile();
            }

            [Argument(0, "Checkpoint file name")]
            [Required]
            internal string Name { get; init; } = string.Empty;

            [Option(Description = "Time between blocks")]
            internal uint SecondsPerBlock { get; }

            [Option(Description = "Enable contract execution tracing")]
            internal bool Trace { get; init; } = false;

            internal Task<int> OnExecuteAsync(CommandLineApplication app, CancellationToken token) => app.ExecuteAsync(this.ExecuteAsync, token);

            internal async Task ExecuteAsync(IFileSystem fileSystem, IConsole console, CancellationToken token)
            {
                if (chain.ConsensusNodes.Count != 1)
                {
                    throw new ArgumentException("Checkpoint create is only supported on single node express instances", nameof(chain));
                }

                var checkpointPath = CheckpointCommand.ResolveFileName(fileSystem, Name);
                console.WriteLine(checkpointPath);
                var consensusContract = chain.GetConsensusContract();
                using var expressStorage = CheckpointExpressStorage.OpenCheckpoint(checkpointPath, chain.Network, chain.AddressVersion, consensusContract.ScriptHash);
                using var expressSystem = new ExpressSystem(chain, chain.ConsensusNodes[0], expressStorage, Trace, SecondsPerBlock);
                await expressSystem.RunAsync(console, token);
            }
        }
    }
}
