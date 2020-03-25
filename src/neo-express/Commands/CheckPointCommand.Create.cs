using McMaster.Extensions.CommandLineUtils;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace NeoExpress.Commands
{
    internal partial class CheckPointCommand
    {
        [Command("create")]
        private class Create
        {
            [Argument(0)]
            private string Name { get; } = string.Empty;

            [Option]
            private string Input { get; } = string.Empty;

            [Option]
            private bool Force { get; }

            private async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);

                    if (chain.ConsensusNodes.Count > 1)
                    {
                        throw new Exception("Checkpoint create is only supported on single node express instances");
                    }

                    var blockchainOperations = new NeoExpress.Neo2.BlockchainOperations();
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
                    console.WriteError(ex.Message);
                    app.ShowHelp();
                    return 1;
                }
            }
        }
    }
}
