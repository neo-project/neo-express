using System;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit.Persistence;
using NeoExpress.Node;

namespace NeoExpress.Commands
{
    [Command("run", Description = "Run Neo-Express instance node")]
    class RunCommand
    {
        readonly IExpressChain expressFile;

        public RunCommand(IExpressChain expressFile)
        {
            this.expressFile = expressFile;
        }

        public RunCommand(CommandLineApplication app) : this(app.GetExpressFile())
        {
        }

        [Argument(0, Description = "Index of node to run")]
        internal int NodeIndex { get; init; } = 0;

        [Option(Description = "Time between blocks")]
        internal uint? SecondsPerBlock { get; }

        [Option(Description = "Discard blockchain changes on shutdown")]
        internal bool Discard { get; init; } = false;

        [Option(Description = "Enable contract execution tracing")]
        internal bool Trace { get; init; } = false;

        internal Task<int> OnExecuteAsync(CommandLineApplication app, CancellationToken token) => app.ExecuteAsync(this.ExecuteAsync, token);

        internal async Task ExecuteAsync(IFileSystem fileSystem, IConsole console, CancellationToken token)
        {
            if (NodeIndex < 0 || NodeIndex >= expressFile.ConsensusNodes.Count)
            {
                throw new Exception("Invalid node index");
            }

            var node = expressFile.ConsensusNodes[NodeIndex];
            var nodePath = fileSystem.GetNodePath(node);
            if (!fileSystem.Directory.Exists(nodePath)) fileSystem.Directory.CreateDirectory(nodePath);

            using var expressStorage = Discard
                ? CheckpointExpressStorage.OpenForDiscard(nodePath)
                : new RocksDbExpressStorage(nodePath);
            //TODO new ExpressSystem(expressFile.Chain
            var expressSystem = new ExpressSystem(expressFile.Chain, node, expressStorage, console, Trace, SecondsPerBlock);
            await expressSystem.RunAsync(token);
        }
    }
}
