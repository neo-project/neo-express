using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    partial class CheckpointCommand
    {
        [Command("create", Description = "Create a new neo-express checkpoint")]
        internal class Create
        {
            readonly IExpressChain chain;

            public Create(IExpressChain chain)
            {
                this.chain = chain;
            }

            public Create(CommandLineApplication app)
            {
                this.chain = app.GetExpressFile();
            }

            [Argument(0, "Checkpoint file name")]
            [Required]
            internal string Name { get; init; } = string.Empty;

            [Option(Description = "Overwrite existing data")]
            internal bool Force { get; }

            internal Task<int> OnExecuteAsync(CommandLineApplication app)
                => app.ExecuteAsync(this.ExecuteAsync);

            internal async Task ExecuteAsync(IFileSystem fileSystem, IConsole console)
            {
                using var expressNode = chain.GetExpressNode();
                var (path, mode) = await ExecuteAsync(expressNode, fileSystem, Name, Force).ConfigureAwait(false);
                console.WriteLine($"Created {fileSystem.Path.GetFileName(path)} checkpoint {mode}");
            }

            public static async Task<(string path, IExpressNode.CheckpointMode checkpointMode)> ExecuteAsync(
                IExpressNode expressNode, IFileSystem fileSystem, string checkpointPath, bool force)
            {
                if (expressNode.Chain.ConsensusNodes.Count != 1)
                {
                    throw new ArgumentException("Checkpoint create is only supported on single node express instances", nameof(chain));
                }

                checkpointPath = CheckpointCommand.ResolveFileName(fileSystem, checkpointPath);
                if (fileSystem.File.Exists(checkpointPath))
                {
                    if (force)
                    {
                        fileSystem.File.Delete(checkpointPath);
                    }
                    else
                    {
                        throw new Exception("You must specify --force to overwrite an existing file");
                    }
                }

                var parentPath = fileSystem.Path.GetDirectoryName(checkpointPath);
                if (!fileSystem.Directory.Exists(parentPath))
                {
                    fileSystem.Directory.CreateDirectory(parentPath);
                }

                var mode = await expressNode.CreateCheckpointAsync(checkpointPath).ConfigureAwait(false);
                return (checkpointPath, mode);
            }
        }
    }
}
