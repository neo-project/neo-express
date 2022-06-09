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

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    // var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    // using var expressNode = chainManager.GetExpressNode();
                    // _ = await chainManager.CreateCheckpointAsync(expressNode, Name, Force, console.Out).ConfigureAwait(false);
                    return 0;
                }
                catch (Exception ex)
                {
                    app.WriteException(ex);
                    return 1;
                }
            }

        }
    }
}
