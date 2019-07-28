using McMaster.Extensions.CommandLineUtils;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using RocksDbSharp;

namespace Neo.Express.Commands
{
    [Command("checkpoint")]
    [Subcommand(typeof(Create), typeof(Restore))]
    internal partial class CheckPointCommand
    {
        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp();
            return 1;
        }
    }
}
