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

            public Create(CommandLineApplication app) : this(app.GetExpressFile())
            {
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
                if (chain.ConsensusNodes.Count != 1)
                {
                    throw new NotSupportedException("Checkpoint create is only supported on single node express instances");
                }

                using var expressNode = chain.GetExpressNode();
                var (checkpointPath, mode) = await ExecuteAsync(expressNode, Name, fileSystem, Force)
                    .ConfigureAwait(false);
                await console.Out.WriteLineAsync($"Created {fileSystem.Path.GetFileName(checkpointPath)} checkpoint {mode}").ConfigureAwait(false);
            }

            public static async Task<(string path, IExpressNode.CheckpointMode checkpointMode)>
                ExecuteAsync(IExpressNode expressNode, string checkpointPath, IFileSystem fileSystem, bool force)
            {
                if (expressNode.Chain.ConsensusNodes.Count != 1)
                {
                    throw new NotSupportedException("Checkpoint create is only supported on single node express instances");
                }

                checkpointPath = fileSystem.ResolveCheckpointFileName(checkpointPath);
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
