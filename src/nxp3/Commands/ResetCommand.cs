using System;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace nxp3.Commands
{
    [Command("reset")]
    class ResetCommand
    {
        [Argument(0)]
        int NodeIndex { get; } = 0;

        [Option]
        string Input { get; } = string.Empty;

        [Option]
        bool Force { get; }

        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            try
            {
                var (chain, _) = Program.LoadExpressChain(Input);

                if (NodeIndex < 0 || NodeIndex >= chain.ConsensusNodes.Count)
                {
                    throw new Exception("Invalid node index");
                }

                if (!Force)
                {
                    throw new Exception("--force must be specified when resetting a node");
                }

                var blockchainOperations = new NeoExpress.Neo3.BlockchainOperations();
                blockchainOperations.ResetNode(chain, NodeIndex);

                return 0;
            }
            catch (Exception ex)
            {
                console.WriteLine(ex.Message);
                return 1;
            }
        }
    }
}
