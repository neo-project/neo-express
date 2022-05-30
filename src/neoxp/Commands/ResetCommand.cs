using System;
using System.IO.Abstractions;
using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit.Models;

namespace NeoExpress.Commands
{
    [Command("reset", Description = "Reset neo-express instance node")]
    class ResetCommand
    {
        readonly IExpressChain chain;

        public ResetCommand(IExpressChain chain)
        {
            this.chain = chain;
        }

        public ResetCommand(CommandLineApplication app) : this(app.GetExpressFile())
        {
        }

        [Argument(0, Description = "Index of node to reset")]
        internal int? NodeIndex { get; }

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

            if (All)
            {
                for (int i = 0; i < chain.ConsensusNodes.Count; i++)
                {
                    ResetNode(fileSystem, chain.ConsensusNodes[i], Force);
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

                ResetNode(fileSystem, chain.ConsensusNodes[nodeIndex], Force);
                console.Out.WriteLine($"node {nodeIndex} reset");
            }
        }

        internal static void ResetNode(IFileSystem fileSystem, ExpressConsensusNode node, bool force)
        {
            if (node.IsRunning())
            {
                var scriptHash = node.Wallet.DefaultAccount?.ScriptHash ?? "<unknown>";
                throw new InvalidOperationException($"node {scriptHash} currently running");
            }

            var nodePath = fileSystem.GetNodePath(node);
            if (fileSystem.Directory.Exists(nodePath))
            {
                if (!force)
                {
                    throw new InvalidOperationException("--force must be specified when resetting a node");
                }

                fileSystem.Directory.Delete(nodePath, true);
            }
        }
    }
}
