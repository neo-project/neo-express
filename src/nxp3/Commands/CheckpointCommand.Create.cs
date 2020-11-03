using System;
using System.IO;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Neo3;

namespace nxp3.Commands
{
    partial class CheckpointCommand
    {
        [Command("create")]
        class Create
        {
            [Argument(0)]
            string Name { get; } = string.Empty;

            [Option]
            string Input { get; } = string.Empty;

            [Option]
            bool Force { get; }

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);

                    if (chain.ConsensusNodes.Count > 1)
                    {
                        throw new Exception("Checkpoint create is only supported on single node express instances");
                    }

                    var blockchainOperations = new BlockchainOperations();
                    var filename = blockchainOperations.ResolveCheckpointFileName(Name);

                    if (File.Exists(filename))
                    {
                        if (!Force)
                        {
                            throw new Exception("You must specify --force to overwrite an existing file");
                        }

                        File.Delete(filename);
                    }

                    await blockchainOperations.CreateCheckpoint(chain, filename, Console.Out);

                    return 0;
                }
                catch (Exception ex)
                {
                    console.WriteLine(ex.Message);
                    app.ShowHelp();
                    return 1;
                }
            }

        }
    }
}
