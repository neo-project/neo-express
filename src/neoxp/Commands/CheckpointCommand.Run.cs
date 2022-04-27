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

namespace NeoExpress.Commands
{
    partial class CheckpointCommand
    {
        [Command("run", Description = "Run a neo-express checkpoint (discarding changes on shutdown)")]
        internal class Run
        {
            readonly IFileSystem fileSystem;

            public Run(IFileSystem fileSystem)
            {
                this.fileSystem = fileSystem;
            }

            [Argument(0, "Checkpoint file name")]
            [Required]
            internal string Name { get; init; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            [Option(Description = "Time between blocks")]
            internal uint SecondsPerBlock { get; }

            [Option(Description = "Enable contract execution tracing")]
            internal bool Trace { get; init; } = false;

            internal async Task ExecuteAsync(IConsole console, CancellationToken token)
            {
                var (chainManager, _) = fileSystem.LoadChainManager(Input, SecondsPerBlock);
                var chain = chainManager;
                if (chain.ConsensusNodes.Count != 1)
                {
                    throw new ArgumentException("Checkpoint create is only supported on single node express instances", nameof(chain));
                }

                var storageProvider = GetCheckpointStorageProvider(chain, Name);
                using var disposable = storageProvider as IDisposable ?? Nito.Disposables.NoopDisposable.Instance;
                await Node.NodeUtility.RunAsync(chain, storageProvider, chain.ConsensusNodes[0], Trace, console.Out, SecondsPerBlock, token);
            }

            internal Neo.Plugins.IStorageProvider GetCheckpointStorageProvider(ExpressChain chain, string checkPointPath)
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

                var wallet = Models.DevWallet.FromExpressWallet(chain.GetProtocolSettings(), node.Wallet);
                var multiSigAccount = wallet.GetMultiSigAccounts().Single();

                return CheckpointStorageProvider.Open(checkPointPath, scriptHash: multiSigAccount.ScriptHash);
            }


            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console, CancellationToken token)
            {
                try
                {
                    await ExecuteAsync(console, token).ConfigureAwait(false);
                    return 0;
                }
                catch (Exception ex)
                {
                    app.WriteException(ex);
                    return 1;
                }
            }
        }
    }
}
