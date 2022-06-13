using System;
using System.IO.Abstractions;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    [Command("checkpoint", Description = "Manage neo-express checkpoints")]
    [Subcommand(typeof(Create), typeof(Restore), typeof(Run))]
    partial class CheckpointCommand
    {
        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp(false);
            return 1;
        }

        internal const string CHECKPOINT_EXTENSION = ".neoxp-checkpoint";

        public static string ResolveFileName(IFileSystem fileSystem, string path)
            => fileSystem.ResolveFileName(path, CHECKPOINT_EXTENSION, () => $"{DateTimeOffset.Now:yyyyMMdd-hhmmss}");
    }
}
