using System;
using System.Diagnostics;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace nxp3.Commands
{
    [Command("reset", "Reset neo-express instance node")]
    class ResetCommand
    {
        [Argument(0, Description = "Index of node to reset")]
        int? NodeIndex { get; }

        [Option(Description = "Path to neo-express data file")]
        string Input { get; } = string.Empty;

        [Option(Description = "Overwrite existing data")]
        bool Force { get; }

        [Option(Description = "Reset all nodes")]
        bool All { get; }

        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            try
            {
                if (NodeIndex.HasValue && All)
                {
                    throw new InvalidOperationException("Only one of NodeIndex or --all can be specified");
                }

                var (chain, _) = Program.LoadExpressChain(Input);
                var blockchainOperations = new NeoExpress.Neo3.BlockchainOperations();

                if (All)
                {
                    for (int i = 0; i < chain.ConsensusNodes.Count; i++)
                    {
                        blockchainOperations.ResetNode(chain, i, Force);
                    }
                }
                else
                {
                    var nodeIndex = NodeIndex.HasValue
                        ? NodeIndex.Value
                        : chain.ConsensusNodes.Count == 1
                            ? 0
                            : throw new InvalidOperationException("node index or --all must be specified when resetting a multi-node chain");

                    blockchainOperations.ResetNode(chain, nodeIndex, Force);
                }

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
