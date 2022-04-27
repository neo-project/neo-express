using System;
using System.IO.Abstractions;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    [Command("reset", Description = "Reset neo-express instance node")]
    class ResetCommand
    {
        readonly IFileSystem fileSystem;

        public ResetCommand(IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        [Argument(0, Description = "Index of node to reset")]
        internal int? NodeIndex { get; }

        [Option(Description = "Path to neo-express data file")]
        internal string Input { get; init; } = string.Empty;

        [Option(Description = "Overwrite existing data")]
        internal bool Force { get; }

        [Option(Description = "Reset all nodes")]
        internal bool All { get; }

        internal void Execute(IConsole console)
        {
            if (NodeIndex.HasValue && All)
            {
                throw new InvalidOperationException("Only one of NodeIndex or --all can be specified");
            }

            var (chainManager, _) = fileSystem.LoadChainManager(Input);
            var chain = chainManager.Chain;

            if (All)
            {
                for (int i = 0; i < chain.ConsensusNodes.Count; i++)
                {
                    fileSystem.ResetNode(chain.ConsensusNodes[i], Force);
                    console.Out.WriteLine($"node {i} reset");
                }
            }
            else
            {
                var nodeIndex = NodeIndex.HasValue
                    ? NodeIndex.Value
                    : chain.ConsensusNodes.Count == 1
                        ? 0
                        : throw new InvalidOperationException("node index or --all must be specified when resetting a multi-node chain");

                fileSystem.ResetNode(chain.ConsensusNodes[nodeIndex], Force);
                console.Out.WriteLine($"node {nodeIndex} reset");
            }
        }

        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            try
            {
                Execute(console);
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
