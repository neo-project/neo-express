using System;
using System.Diagnostics;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    [Command("reset", "Reset neo-express instance node")]
    class ResetCommand
    {
        readonly IBlockchainOperations chainManager;

        public ResetCommand(IBlockchainOperations chainManager)
        {
            this.chainManager = chainManager;
        }

        [Argument(0, Description = "Index of node to reset")]
        int? NodeIndex { get; }

        [Option(Description = "Path to neo-express data file")]
        string Input { get; } = string.Empty;

        [Option(Description = "Overwrite existing data")]
        bool Force { get; }

        [Option(Description = "Reset all nodes")]
        bool All { get; }

        internal void Execute(IConsole console)
        {
            if (NodeIndex.HasValue && All)
            {
                throw new InvalidOperationException("Only one of NodeIndex or --all can be specified");
            }

            var (chain, _) = chainManager.Load(Input);

            if (All)
            {
                for (int i = 0; i < chain.ConsensusNodes.Count; i++)
                {
                    chainManager.Reset(chain.ConsensusNodes[i], Force);
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

                chainManager.Reset(chain.ConsensusNodes[nodeIndex], Force);
                console.Out.WriteLine($"node {nodeIndex} reset");
            }
        }

        internal int OnExecute(IConsole console)
        {
            try
            {
                Execute(console);
                return 0;
            }
            catch (Exception ex)
            {
                console.Error.WriteLine(ex.Message);
                return 1;
            }
        }
    }
}
