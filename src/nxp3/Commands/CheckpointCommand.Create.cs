using System;
using System.IO;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Neo3;

namespace nxp3.Commands
{
    partial class CheckpointCommand
    {
        [Command("create", Description = "Create a new neo-express checkpoint")]
        class Create
        {
            [Argument(0, "Checkpoint file name")]
            string Name { get; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            [Option(Description = "Overwrite existing data")]
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

                    var parentPath = Path.GetDirectoryName(filename);
                    if (!Directory.Exists(parentPath))
                    {
                        Directory.CreateDirectory(parentPath);
                    }

                    await blockchainOperations.CreateCheckpoint(chain, filename, Console.Out);

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
}
