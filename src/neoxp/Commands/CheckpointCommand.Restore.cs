using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    partial class CheckpointCommand
    {
        [Command("restore", Description = "Restore a neo-express checkpoint")]
        class Restore
        {
            readonly IExpressChainManagerFactory chainManagerFactory;

            public Restore(IExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, "Checkpoint file name")]
            [Required]
            string Name { get; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            [Option(Description = "Overwrite existing data")]
            bool Force { get; }

            internal int OnExecute(IConsole console)
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
                    console.Error.WriteLine(ex.Message);
                    return 1;
                }
            }
        }
    }
}
