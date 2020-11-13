using System;
using System.Diagnostics;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace nxp3.Commands
{
    [Command("reset")]
    class ResetCommand
    {
        [Argument(0)]
        int? NodeIndex { get; }

        [Option]
        string Input { get; } = string.Empty;

        [Option]
        bool Force { get; }

        [Option]
        bool All { get; }

        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            try
            {
                if (NodeIndex.HasValue && All)
                {
                    throw new Exception("Only one of NodeIndex or --all can be specified");
                }

                if (!NodeIndex.HasValue && !All)
                {
                    throw new Exception("Either NodeIndex or --all must be specified");
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
                    Debug.Assert(NodeIndex.HasValue);
                    blockchainOperations.ResetNode(chain, NodeIndex.Value, Force);
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
