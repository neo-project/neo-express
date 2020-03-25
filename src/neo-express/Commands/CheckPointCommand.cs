using McMaster.Extensions.CommandLineUtils;
using System;
using System.IO;

namespace NeoExpress.Commands
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
