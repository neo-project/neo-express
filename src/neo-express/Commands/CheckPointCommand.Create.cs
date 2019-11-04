using McMaster.Extensions.CommandLineUtils;
using System;
using System.IO;
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

            [Option]
            private bool Online { get; }

            private async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var filename = string.IsNullOrEmpty(Name)
                        ? $"{DateTimeOffset.Now:yyyyMMdd-hhmmss}{CHECKPOINT_EXTENSION}"
                        : Name + CHECKPOINT_EXTENSION;

                    filename = Path.GetFullPath(filename);

                    if (File.Exists(filename))
                    {
                        if (!Force)
                        {
                            throw new Exception("You must specify --force to overwrite an existing file");
                        }

                        File.Delete(filename);
                    }

                    var (chain, _) = Program.LoadExpressChain(Input);

                    if (chain.ConsensusNodes.Count > 1)
                    {
                        throw new Exception("Checkpoint create is only supported on single node express instances");
                    }

                    //if (Online)
                    //{
                    //    var uri = chain.GetUri();
                    //    var result = await NeoRpcClient.ExpressCreateCheckpoint(uri, filename)
                    //        .ConfigureAwait(false);
                    //    console.WriteResult(result);
                    //}
                    //else
                    //{
                    //    var blockchainPath = chain.ConsensusNodes[0].GetBlockchainPath();

                    //    Program.BlockchainOperations.CreateCheckpoint(
                    //        chain, blockchainPath, filename);
                    //}

                    console.WriteLine($"created checkpoint {Path.GetFileName(filename)}");

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
