using McMaster.Extensions.CommandLineUtils;
using System.IO;
using System;

namespace NeoExpress.Commands
{
    [Command("run")]
    internal class RunCommand
    {
        [Argument(0)]
        private int? NodeIndex { get; }

        [Option]
        private string Input { get; }

        [Option]
        private uint SecondsPerBlock { get; }

        [Option]
        private bool Reset { get; }

        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            try
            {
                var (chain, _) = Program.LoadExpressChain(Input);
                var index = NodeIndex.GetValueOrDefault();

                if (!NodeIndex.HasValue && chain.ConsensusNodes.Count > 1)
                {
                    throw new Exception("Node index not specified");
                }

                if (index >= chain.ConsensusNodes.Count || index < 0)
                {
                    throw new Exception("Invalid node index");
                }

                var node = chain.ConsensusNodes[index];
                var folder = node.GetBlockchainPath();

                if (Reset && Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }

                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                var cts = BlockchainOperations.RunBlockchain(
                    folder, chain, index, SecondsPerBlock, console.Out);
                console.CancelKeyPress += (sender, args) => cts.Cancel();
                cts.Token.WaitHandle.WaitOne();
                return 0;
            }
            catch (Exception ex)
            {
                console.WriteError(ex.Message);
                app.ShowHelp();
                return 1;
            }
        }
    }
}