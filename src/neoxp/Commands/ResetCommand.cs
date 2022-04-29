using System;
using System.IO.Abstractions;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    [Command("reset", Description = "Reset neo-express instance node")]
    class ResetCommand
    {
        readonly IExpressFile expressFile;

        public ResetCommand(IExpressFile expressFile)
        {
            this.expressFile = expressFile;
        }

        public ResetCommand(CommandLineApplication app) : this(app.GetExpressFile())
        {
        }


        [Argument(0, Description = "Index of node to reset")]
        internal int? NodeIndex { get; }

        
        internal string Input { get; init; } = string.Empty;

        [Option(Description = "Overwrite existing data")]
        internal bool Force { get; }

        [Option(Description = "Reset all nodes")]
        internal bool All { get; }

        internal int OnExecute(CommandLineApplication app) => app.Execute(this.Execute);

        internal void Execute(IFileSystem fileSystem, IConsole console)
        {
            if (NodeIndex.HasValue && All)
            {
                throw new InvalidOperationException("Only one of NodeIndex or --all can be specified");
            }

            var chain = expressFile.Chain;

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
    }
}
