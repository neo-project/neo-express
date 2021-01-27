using System;
using System.IO;
using McMaster.Extensions.CommandLineUtils;


namespace NeoExpress.Commands
{
    [Command("export", Description = "Export neo-express protocol, config and wallet files")]
    class ExportCommand
    {
        readonly IBlockchainOperations chainManager;

        public ExportCommand(IBlockchainOperations chainManager)
        {
            this.chainManager = chainManager;
        }

        [Option(Description = "Path to neo-express data file")]
        string Input { get; } = string.Empty;

        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            try
            {
                var (chain, _) = chainManager.LoadChain(Input);
                var password = Prompt.GetPassword("Input password to use for exported wallets");
                chainManager.ExportChain(chain, password);
                foreach (var node in chain.ConsensusNodes)
                {
                    console.Out.WriteLine($"Exported {node.Wallet.Name} Conensus Node config + wallet");
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
