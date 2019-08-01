using McMaster.Extensions.CommandLineUtils;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using RocksDbSharp;

namespace Neo.Express.Commands
{
    [Command("checkpoint")]
    [Subcommand(typeof(Create), typeof(Restore), typeof(Run))]
    internal partial class CheckPointCommand
    {
        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteError("You must specify at a subcommand.");
            app.ShowHelp();
            return 1;
        }
    }
}
