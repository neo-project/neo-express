using System;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;

namespace NeoExpress.Commands
{
    [Command("run", Description = "Run Neo-Express instance node")]
    class RunCommand
    {
        readonly IFileSystem fileSystem;

        public RunCommand(IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        [Argument(0, Description = "Index of node to run")]
        internal int NodeIndex { get; init; } = 0;

        [Option(Description = "Path to neo-express data file")]
        internal string Input { get; init; } = string.Empty;

        [Option(Description = "Time between blocks")]
        internal uint? SecondsPerBlock { get; }

        [Option(Description = "Discard blockchain changes on shutdown")]
        internal bool Discard { get; init; } = false;

        [Option(Description = "Enable contract execution tracing")]
        internal bool Trace { get; init; } = false;

        internal async Task ExecuteAsync(IConsole console, CancellationToken token)
        {
            var (chainManager, _) = fileSystem.LoadChainManager(Input, SecondsPerBlock);
            var chain = chainManager.Chain;

            if (NodeIndex < 0 || NodeIndex >= chain.ConsensusNodes.Count) throw new Exception("Invalid node index");

            var node = chain.ConsensusNodes[NodeIndex];
            var nodePath = fileSystem.GetNodePath(node);
            if (!fileSystem.Directory.Exists(nodePath)) fileSystem.Directory.CreateDirectory(nodePath);

            var storageProvider = Discard
                ? RocksDbStorageProvider.OpenForDiscard(nodePath)
                : RocksDbStorageProvider.Open(nodePath);

            using var disposable = storageProvider as IDisposable ?? Nito.Disposables.NoopDisposable.Instance;
            await Node.NodeUtility.RunAsync(chain, storageProvider, chain.ConsensusNodes[0], Trace, console.Out, SecondsPerBlock, token);
        }

        internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console, CancellationToken token)
        {
            try
            {
                await ExecuteAsync(console, token);
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
