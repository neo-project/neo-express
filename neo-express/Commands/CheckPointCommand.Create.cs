using McMaster.Extensions.CommandLineUtils;
using System;
using System.IO;
using System.IO.Compression;

namespace Neo.Express.Commands
{
    internal partial class CheckPointCommand
    {
        [Command("create")]
        private class Create
        {
            [Argument(0)]
            private string Name { get; }

            [Option]
            private string Input { get; }

            [Option]
            private bool Force { get; }

            private int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var filename = string.IsNullOrEmpty(Name)
                        ? $"{DateTimeOffset.Now:yyyyMMdd-hhmmss}.neo-express"
                        : Name + ".neo-express";

                    if (!Force && File.Exists(filename))
                    {
                        throw new Exception("You must specify --force to overwrite an existing file");
                    }

                    var (devChain, _) = DevChain.Load(Input);

                    if (devChain.ConsensusNodes.Count > 1)
                    {
                        throw new Exception("Checkpoint create and restore is only supported on single node express instances");
                    }

                    var consensusNode = devChain.ConsensusNodes[0];
                    var blockchainPath = consensusNode.BlockchainPath;

                    string checkpointTempPath = Path.Combine(
                        Path.GetTempPath(), Path.GetRandomFileName());

                    using (var db = new Persistence.DevStore(blockchainPath))
                    {
                        db.CheckPoint(checkpointTempPath);
                        ZipFile.CreateFromDirectory(checkpointTempPath, filename);
                        console.WriteLine($"created checkpoint {Path.GetFileName(filename)}");
                    }

                    Directory.Delete(checkpointTempPath, true);

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
