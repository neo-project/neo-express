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
            readonly ExpressChainManagerFactory chainManagerFactory;

            public Restore(ExpressChainManagerFactory chainManagerFactory)
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

            internal int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    chainManager.RestoreCheckpoint(Name, Force);
                    console.WriteLine($"Checkpoint {Name} successfully restored");
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
