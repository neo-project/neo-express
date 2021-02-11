using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    partial class CheckpointCommand
    {
        [Command("create", Description = "Create a new neo-express checkpoint")]
        internal class Create
        {
            readonly IExpressChainManagerFactory chainManagerFactory;

            public Create(IExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, "Checkpoint file name")]
            [Required]
            internal string Name { get; init; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            [Option(Description = "Overwrite existing data")]
            internal bool Force { get; }

            internal static async Task ExecuteAsync(IExpressChainManager chainManager, IExpressNode expressNode, string name, bool force, System.IO.TextWriter writer)
            {
                var (path, online) = await chainManager.CreateCheckpointAsync(expressNode, name, force).ConfigureAwait(false);
                await writer.WriteLineAsync($"Created {System.IO.Path.GetFileName(path)} checkpoint {(online ? "online" : "offline")}").ConfigureAwait(false);
            }

            internal async Task<int> OnExecuteAsync(IConsole console)
            {
                try
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    using var expressNode = chainManager.GetExpressNode();
                    await ExecuteAsync(chainManager, expressNode, Name, Force, console.Out).ConfigureAwait(false);
                    return 0;
                }
                catch (Exception ex)
                {
                    await console.Error.WriteLineAsync(ex.Message);
                    return 1;
                }
            }

        }
    }
}
