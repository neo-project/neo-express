using System;
using System.ComponentModel.DataAnnotations;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    partial class CheckpointCommand
    {
        [Command("restore", Description = "Restore a neo-express checkpoint")]
        internal class Restore
        {
            readonly IExpressChain chain;

            public Restore(IExpressChain chain)
            {
                this.chain = chain;
            }

            public Restore(CommandLineApplication app)
            {
                this.chain = app.GetExpressFile();
            }

            [Argument(0, "Checkpoint file name")]
            [Required]
            internal string Name { get; init; } = string.Empty;

            [Option(Description = "Overwrite existing data")]
            internal bool Force { get; }

            internal int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    // var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    // chainManager.RestoreCheckpoint(Name, Force);
                    // console.WriteLine($"Checkpoint {Name} successfully restored");
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
