using McMaster.Extensions.CommandLineUtils;
using System.IO.Compression;
using System;
using System.IO;

namespace Neo.Express.Commands
{
    [Command("checkpoint")]
    [Subcommand(typeof(Create), typeof(Restore), typeof(Run))]
    internal partial class CheckPointCommand
    {
        private const string CHECKPOINT_EXTENSION = ".neo-express-checkpoint";
        private const string ADDRESS_FILENAME = "ADDRESS.neo-express";

        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteError("You must specify at a subcommand.");
            app.ShowHelp();
            return 1;
        }

        private static string ValidateCheckpointFileName(string name)
        {
            var filename = name;

            if (!File.Exists(filename))
            {
                filename = name + CHECKPOINT_EXTENSION;
            }

            if (!File.Exists(filename))
            {
                throw new Exception($"Checkpoint {name} couldn't be found");
            }

            return filename;
        }
    }
}
